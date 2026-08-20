# Vinto — Backend

API REST multi-tenant para tiendas online: catálogo con variantes, control de stock, pedidos, descuentos y cupones, cobros con Mercado Pago y panel de administración en tiempo real. Un despliegue sirve a N negocios independientes, cada uno con su propia tienda pública.

- **Demo en producción:** https://vintoapp.com
- **Frontend (repositorio separado):** https://github.com/FranBover/vinto-frontend — React + TypeScript, Vite, Zustand, TailwindCSS

---

## El problema

Nació para un negocio de comida donde todos los pedidos entraban por teléfono: alguien anotaba a mano, se equivocaba con los precios, no quedaba registro de nada y no había forma de saber qué se vendía más. La primera versión resolvía ese caso puntual.

Al generalizarlo quedó claro que el problema no era la comida: era que cualquier negocio chico necesita vender online sin montar infraestructura propia. Hoy el dominio es genérico —productos, variantes, stock, cupones, pedidos— y sirve para cualquier rubro que venda productos. El cliente final compra sin registrarse, y el dueño del negocio gestiona todo desde un panel que recibe los pedidos en el momento.

---

## Stack

| Capa | Tecnología |
|------|-----------|
| Runtime | .NET 9 / ASP.NET Core Web API |
| Datos | Entity Framework Core 9 + SQL Server |
| Autenticación | JWT Bearer (HMAC-SHA256) |
| Tiempo real | SignalR |
| Pagos | Mercado Pago SDK 2.12.1 (OAuth + Checkout Pro + webhooks) |
| Imágenes | SixLabors.ImageSharp 3.1.12 |
| Almacenamiento | Azure Blob Storage / disco local (intercambiable) |
| Documentación | Swagger / OpenAPI (Swashbuckle 6.6.2) |
| CI/CD | Azure Pipelines → Azure App Service (Linux) |

---

## Arquitectura

Arquitectura en capas con inyección de dependencias. Cada capa depende de interfaces, no de implementaciones, lo que permite reemplazar piezas sin tocar el resto: el proveedor de almacenamiento se elige por configuración y el resto del código no se entera.

```
Controllers  →  Services  →  Repositories  →  EF Core  →  SQL Server
   HTTP          Negocio        Datos
```

```
Eat_Experience.sln
└── Eat_Experience/                    proyecto Vinto.Api
    ├── Controllers/                   16 controllers: HTTP, extracción del tenant, validación
    ├── Services/
    │   ├── Interfaces/                contratos
    │   └── Implementaciones/          17 servicios: toda la lógica de negocio
    ├── Repositories/
    │   ├── Interfaces/
    │   └── Implementaciones/          acceso a datos sobre AppDbContext
    ├── DTOs/                          contratos de request/response (nunca se expone la entidad)
    ├── Models/                        entidades de dominio
    ├── Data/AppDbContext.cs           relaciones, índices, precisión decimal, defaults
    ├── Migrations/                    22 migraciones de EF Core
    ├── Hubs/PedidosHub.cs             SignalR: notificaciones al panel
    ├── Storage/                       IStorageProvider + Local + AzureBlob
    ├── Helpers/                       cifrado AES-GCM, validador de firma de webhooks
    └── Program.cs                     composición: DI, auth, CORS, middleware
```

---

## Decisiones técnicas

### Multi-tenant sin base de datos por cliente

Un solo esquema compartido, con `AdministradorId` como discriminador en cada entidad de negocio. Se descartó una base por tenant porque multiplica el costo de infraestructura y el de aplicar migraciones, que era desproporcionado para negocios chicos.

El tenant se resuelve de dos formas distintas según el contexto:

- **Zona privada:** del claim `adminId` del JWT. El cliente nunca envía su propio `adminId`; sale del token firmado, así que no se puede falsear sin la clave.
- **Zona pública:** de un *slug* derivado del nombre del negocio (`/api/public/locales/{slug}/menu`). El comprador no tiene cuenta ni token, y la URL es lo único que identifica la tienda.

Los controllers privados extraen el `adminId` con un helper `TryGetAdminId` y cada operación verifica que la entidad pertenezca a ese tenant antes de tocarla; si no, devuelve `403`. La unicidad también es por tenant: los índices son compuestos (`AdministradorId` + `Nombre`), de modo que dos negocios pueden tener un producto con el mismo nombre sin colisionar.

**Limitación conocida y asumida:** los filtros globales de EF Core (`HasQueryFilter`) están preparados pero comentados en `AppDbContext`, porque activarlos requiere inyectar un `ITenantProvider` en el contexto. Hoy el aislamiento depende de filtrar a mano en cada consulta. Funciona, pero un olvido sería una fuga entre tenants. Está primero en el roadmap.

### Tiempo real: el grupo sale del token, no del cliente

Un pedido que entra tiene que aparecer en el panel sin que nadie recargue nada. Con *polling* la elección era entre latencia alta o castigar la base con consultas constantes, así que se usó SignalR con WebSockets.

Cada conexión se suscribe a un grupo identificado por el `adminId`, y cuando entra un pedido el evento se emite solo a ese grupo. La decisión que importa está en cómo se arma el grupo: el `PedidosHub` lo deriva del claim del token en `OnConnectedAsync`, en vez de exponer un método `JoinGroup(adminId)` que el cliente invoque. Si el cliente eligiera su grupo, cualquiera con un token válido podría suscribirse al de otro negocio y espiar sus pedidos en vivo. Derivándolo del token, eso es imposible.

Hay un detalle de infraestructura: el navegador no puede mandar el header `Authorization` en el handshake de un WebSocket. Se resuelve en `Program.cs` con un handler de `OnMessageReceived` que lee el token del query string `access_token`, pero **solo** para rutas bajo `/hubs`; el resto de la API sigue exigiendo el header.

Eventos que emite el servidor: `NuevoPedido` y `PagoConfirmado`.

### Pagos: cada negocio cobra en su propia cuenta

El requisito era que la plata vaya directo a la cuenta de cada negocio, sin pasar por una cuenta central. Eso obliga a OAuth: cada tenant autoriza la aplicación y el backend guarda sus credenciales de Mercado Pago.

Guardar tokens de terceros en la base cambia el modelo de amenaza: una copia de la base deja de ser un problema de datos y pasa a ser acceso a las cuentas de cobro de los clientes. Por eso los `AccessToken` y `RefreshToken` se cifran con **AES-GCM de 256 bits** antes de persistirse (`EncryptionHelper`). Se eligió GCM y no CBC porque es cifrado autenticado: detecta manipulación del ciphertext, no solo lo oculta. Cada operación genera un nonce aleatorio de 12 bytes, y el formato almacenado es `nonce + tag + ciphertext` en base64. La clave vive en configuración, nunca en el código.

Tres puntos más del flujo:

- **CSRF en el OAuth:** el parámetro `state` es aleatorio de 32 bytes, se guarda en `IMemoryCache` asociado al `adminId` con TTL, y se invalida apenas se usa. Sin eso, un atacante podría inducir a un negocio a vincular la cuenta de Mercado Pago del atacante.
- **Firma de webhooks:** los webhooks entrantes se validan por HMAC contra el header `x-signature` (`MercadoPagoSignatureValidator`). Un endpoint público que marca pedidos como pagados sin verificar firma es fraude directo.
- **Idempotencia:** Mercado Pago reintenta los webhooks. La protección es un índice único sobre `(PaymentId, Status)` en `PagoMercadoPago`: la garantía la da la base, no un `if` en el código que dos requests concurrentes podrían atravesar a la vez.

### Almacenamiento intercambiable

En Azure App Service el disco es efímero: lo que se escribe se pierde en cada reinicio o redeploy. Guardar imágenes en disco funciona en desarrollo y falla en producción.

La abstracción `IStorageProvider` tiene dos implementaciones —`LocalStorageProvider` y `AzureBlobStorageProvider`— y `Program.cs` registra una u otra según `Storage:Provider`. El resto del código depende solo de la interfaz. Desarrollo usa disco sin necesitar credenciales de Azure; producción usa Blob Storage.

Las imágenes se normalizan al subirlas, en vez de servir lo que mande el usuario: se valida el content type contra una lista blanca, se rechaza todo lo que supere 5 MB, se redimensiona a 1200px de ancho máximo y se convierte a **WebP** con calidad 85. Un catálogo con fotos sacadas del celular es la diferencia entre una tienda que carga y una que no.

### Separación en capas y DTOs

Controllers, services y repositories separados por interfaces. El objetivo concreto: la lógica de precios y descuentos no debe vivir en un controller, porque se necesita desde el endpoint público de creación de pedidos y desde el panel privado.

Las entidades nunca salen por la API: todo pasa por DTOs. Devolver la entidad `Administrador` directamente expondría el `PasswordHash` y los tokens de Mercado Pago.

**Deuda reconocida:** la capa de repositorios es parcial. Varios servicios y controllers usan `AppDbContext` directamente cuando necesitan consultas con múltiples `Include` —el menú público es el caso más claro— porque envolverlas en el repositorio agregaba indirección sin beneficio. Conviven los dos estilos y no hay una convención única.

### Cálculo de descuentos con orden explícito

Los descuentos se aplican en un orden definido y documentado (`DescuentoCalculatorService`): primero los de producto, después los de categoría sobre el precio ya reducido, y por último los de pedido completo sobre el subtotal. Dentro de cada nivel se ordena por fecha de creación, para que el resultado sea determinista y no dependa del orden en que la base devuelva las filas.

Hay un *clamp*: ningún precio unitario baja de 0.01 y el subtotal global tampoco queda negativo. Sin eso, dos descuentos acumulados podrían generar totales negativos.

Cada descuento aplicado se persiste como snapshot en `DetallePedidoDescuento`, con el nombre y el monto congelados al momento de la venta. Si después se edita o se borra la regla, los pedidos históricos siguen siendo auditables.

### Errores que no rompen CORS

El middleware global de excepciones se registra **por encima** de `UseCors` a propósito. Una excepción no controlada devolvía un 500 sin headers CORS, y el navegador lo mostraba como un error de CORS en vez del error real, lo que hacía perder tiempo en cada diagnóstico. El middleware deliberadamente no llama a `Response.Clear()`, para no borrar los headers que CORS ya aplicó.

El cuerpo de la respuesta está condicionado al entorno: en producción solo un mensaje genérico; fuera de producción incluye mensaje, detalle y tipo. La excepción completa siempre queda en los logs del servidor.

---

## Modelo de datos

`Administrador` es el tenant: contiene los datos del negocio (nombre, dirección, horarios, zona y costo de envío, alias de transferencia) y sus credenciales cifradas de Mercado Pago. Todo lo demás cuelga de ahí.

**Catálogo**

- `Categoria` — agrupa productos, con orden configurable. Única por `(AdministradorId, Nombre)`.
- `Producto` — precio, disponibilidad, stock simple, y bandera `TieneVariantes`.
- `ProductoExtra` — adicionales opcionales con precio propio.
- `TipoVariante` / `OpcionVariante` / `VarianteProducto` — modelo de variantes en dos niveles: se definen hasta 2 tipos por producto (por ejemplo "Talle" y "Color") con sus opciones, y `VarianteProducto` es cada combinación concreta con su propio precio, stock y SKU. Se generan por combinatoria desde un endpoint.

**Ventas**

- `Pedido` — cliente, forma de pago y entrega, estados (`Pendiente → Confirmado → EnPreparacion → Listo → Entregado`, más `Cancelado`), desglose de totales, código de seguimiento y campos de Mercado Pago.
- `DetallePedido` / `DetallePedidoExtra` — líneas del pedido con precio unitario congelado y sus adicionales.
- `ComentarioPedido` — notas internas del negocio sobre un pedido.

**Promociones**

- `Descuento` — reglas automáticas por producto, categoría o pedido completo, con vigencia por fechas.
- `Cupon` / `UsoCupon` — códigos con límite de usos, pedido mínimo y vencimiento. `UsoCupon` registra cada canje y permite liberarlo si el pedido se cancela.
- `DetallePedidoDescuento` — snapshot de lo aplicado en cada venta.

**Operaciones**

- `MovimientoStock` — auditoría de cada cambio de stock (entrada, salida, ajuste) con stock anterior y nuevo.
- `Imagen` — metadatos de archivos subidos, asociados a producto, categoría o logo.
- `PagoMercadoPago` — auditoría de webhooks, con el payload crudo y el índice único que garantiza idempotencia.

Decisiones del esquema: todo campo monetario es `decimal(18,2)`, nunca punto flotante. Las fechas se guardan en UTC con `GETUTCDATE()` como default y se convierten a horario argentino solo en los reportes. Los `DeleteBehavior` son mayormente `Restrict` para no perder historial: un producto vendido alguna vez no se puede borrar en cascada.

---

## API

Base: `/api`. Los endpoints privados requieren `Authorization: Bearer <token>`.

### Autenticación

| Método | Endpoint | Auth | Descripción |
|--------|----------|------|-------------|
| POST | `/api/Auth/register` | `X-Admin-Key` | Registra un negocio nuevo |
| POST | `/api/Auth/login` | — | Devuelve el JWT |

### Público (sin autenticación)

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/public/locales/{slug}/menu` | Catálogo completo con precios ya descontados |
| POST | `/api/public/locales/{slug}/pedidos` | Crea un pedido y devuelve código de seguimiento |
| POST | `/api/public/locales/{slug}/pedidos/{pedidoId}/preferencia-mp` | Genera la preferencia de pago |
| GET | `/api/public/pedidos/{codigoSeguimiento}/estado-pago` | Seguimiento del pedido |
| POST | `/api/public/locales/{slug}/cupones/validar` | Valida un cupón contra un subtotal |
| GET | `/api/MercadoPago/oauth/callback` | Callback de OAuth (protegido por `state`) |
| POST | `/api/MercadoPago/webhook` | Webhook de pagos (protegido por firma HMAC) |

### Negocio

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/Administrador/{id}` | Datos del negocio |
| PATCH | `/api/Administrador/{id}/local` | Actualización parcial |

### Catálogo

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET / POST | `/api/Categorias` | Lista / crea |
| GET / PUT / DELETE | `/api/Categorias/{id}` | Obtiene / actualiza / elimina |
| PATCH | `/api/Categorias/reordenar` | Reordena todas las categorías |
| GET / POST | `/api/Productos` | Lista / crea |
| GET / PUT / DELETE | `/api/Productos/{id}` | Obtiene / actualiza / elimina |
| PATCH | `/api/Productos/{id}/disponibilidad` | Habilita o deshabilita |
| GET / POST | `/api/ProductoExtra` | Lista / crea adicionales |
| GET / PUT / DELETE | `/api/ProductoExtra/{id}` | Obtiene / actualiza / elimina |
| GET | `/api/ProductoExtra/por-producto/{productoId}` | Adicionales de un producto |

### Variantes

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET / POST | `/api/Productos/{productoId}/tipos-variante` | Lista / crea tipo (máximo 2) |
| PUT / DELETE | `/api/Productos/{productoId}/tipos-variante/{id}` | Actualiza / elimina tipo |
| GET / POST | `/api/tipos-variante/{tipoId}/opciones` | Lista / crea opción |
| PUT / DELETE | `/api/tipos-variante/{tipoId}/opciones/{id}` | Actualiza / elimina opción |
| GET | `/api/Productos/{productoId}/variantes` | Lista combinaciones |
| POST | `/api/Productos/{productoId}/variantes/generar` | Genera por combinatoria |
| DELETE | `/api/Productos/{productoId}/variantes` | Elimina todas |
| PUT / DELETE | `/api/Variantes/{varianteId}` | Actualiza / elimina una |

### Stock

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/Productos/{productoId}/stock` | Estado y últimos movimientos |
| POST | `/api/Productos/{productoId}/stock/ajustar` | Fija el stock a un valor |
| POST | `/api/Productos/{productoId}/stock/agregar` | Repone |
| GET | `/api/Stock/alertas` | Productos con stock bajo o agotado |

### Promociones

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET / POST | `/api/Descuentos` | Lista (filtra por `activo`) / crea |
| GET / PUT | `/api/Descuentos/{id}` | Obtiene / actualiza |
| GET / POST | `/api/Cupones` | Lista (filtra por `activo`) / crea |
| GET / PUT | `/api/Cupones/{id}` | Obtiene / actualiza |
| GET | `/api/Cupones/{id}/metricas` | Métricas de uso |

### Pedidos

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| GET | `/api/Pedidos` | Lista con filtros por estado, fechas, forma de pago y entrega |
| GET | `/api/Pedidos/{id}` | Detalle con desglose de totales |
| PATCH | `/api/Pedidos/{id}/estado` | Cambia estado (descuenta o repone stock, gestiona cupón) |
| GET | `/api/Pedidos/{id}/resumen` | Resumen formateado para WhatsApp |
| GET | `/api/Pedidos/{id}/comanda` | Vista de preparación |
| GET | `/api/Pedidos/{id}/ticket` | Ticket con totales y vuelto |
| GET / POST | `/api/Pedidos/{id}/comentarios` | Lista / agrega nota interna |

### Imágenes, pagos y reportes

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| POST | `/api/Imagenes/upload` | Sube imagen (multipart) |
| GET | `/api/Imagenes` | Lista por `tipo` y `entidadId` |
| DELETE | `/api/Imagenes/{id}` | Elimina |
| GET | `/api/MercadoPago/oauth/url` | URL de autorización |
| GET | `/api/MercadoPago/estado` | Estado de la conexión |
| GET | `/api/MercadoPago/diagnostico` | Diagnóstico de token y pedidos pendientes |
| POST | `/api/MercadoPago/desconectar` | Desvincula la cuenta |
| GET | `/api/Reportes/dashboard?periodo=` | Ventas por `hoy`, `semana`, `mes` o `anio` |

### SignalR

`/hubs/pedidos` — el token viaja por query string `access_token`. Eventos servidor → cliente: `NuevoPedido`, `PagoConfirmado`.

---

## Cómo levantarlo

**Requisitos:** .NET 9 SDK y SQL Server (o SQL Server Express).

**1. Clonar y ubicarse en el proyecto**

```bash
git clone https://github.com/FranBover/vinto-backend-v2.git
cd vinto-backend-v2/Eat_Experience
```

**2. Crear `appsettings.Development.json`**

Este archivo está en `.gitignore` y nunca se versiona. Configuración mínima para arrancar:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=VintoDb;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "Key": "CLAVE_DE_AL_MENOS_32_CARACTERES",
    "Issuer": "VintoAPI",
    "Audience": "VintoFrontend",
    "ExpirationInMinutes": 480
  },
  "AdminRegistroKey": "CLAVE_PARA_HABILITAR_EL_REGISTRO",
  "Encryption": {
    "Key": "CLAVE_DE_32_BYTES_EN_BASE64"
  },
  "Storage": {
    "Provider": "Local",
    "Local": { "BasePath": "uploads" }
  }
}
```

`Encryption:Key` debe ser exactamente 32 bytes en base64 o la aplicación falla al iniciar. Para generarla:

```bash
openssl rand -base64 32
```

**3. Aplicar las migraciones**

```bash
dotnet tool install --global dotnet-ef    # si no lo tenés
dotnet ef database update
```

No hay `Database.Migrate()` automático al arrancar: las migraciones se aplican de forma explícita.

**4. Ejecutar**

```bash
dotnet run
```

Queda en `http://localhost:5202` y `https://localhost:7288`. Swagger en `/swagger`, solo en Development.

**5. Primer usuario**

Al iniciar en Development, si no hay ningún administrador en la base, se crea uno de prueba automáticamente. Las credenciales están en el bloque de seed al final de `Program.cs`. Este seed está condicionado a `IsDevelopment()`, así que nunca se ejecuta en producción.

Para crear un negocio real, usar `POST /api/Auth/register` con el header `X-Admin-Key` igual al `AdminRegistroKey` configurado.

### Configuración de Mercado Pago (opcional)

Sin estas claves la aplicación levanta y funciona, pero los endpoints de pago fallan al usarse. Se obtienen en el [panel de desarrolladores de Mercado Pago](https://www.mercadopago.com.ar/developers):

```json
{
  "MercadoPago": {
    "ClientId": "",
    "ClientSecret": "",
    "WebhookSecret": "",
    "RedirectUri": "https://TU_DOMINIO/api/mercadopago/oauth/callback",
    "AuthBaseUrl": "https://auth.mercadopago.com.ar",
    "ApiBaseUrl": "https://api.mercadopago.com",
    "BackendUrl": "https://TU_DOMINIO",
    "FrontendAdminUrl": "http://localhost:5173/admin/mi-local",
    "FrontendClientUrl": "http://localhost:5173"
  }
}
```

El OAuth y los webhooks necesitan una URL pública alcanzable por Mercado Pago; en local se resuelve con un túnel (ngrok o similar) apuntado al puerto de la API.

### Para usar Azure Blob Storage

```json
{
  "Storage": {
    "Provider": "AzureBlob",
    "AzureBlob": {
      "ConnectionString": "",
      "ContainerName": ""
    }
  }
}
```

---

## Deploy

CI/CD con **Azure Pipelines** (`azure-pipelines.yml` en la raíz). Se dispara con cada push a `main`:

1. Agente `ubuntu-latest`, instala el SDK de .NET 9.
2. `dotnet publish` de `Vinto.Api.csproj` en configuración Release, empaquetado en zip.
3. Despliegue a Azure App Service Linux `vinto-carripollo-api-dev-linux`, con `dotnet Vinto.Api.dll` como comando de arranque.

El pipeline se migró de Windows a Linux: `VSBuild` con paquete de WebDeploy se reemplazó por `dotnet publish`, que es más rápido, más barato en agentes y no depende de MSBuild.

Los secretos no viajan en el repositorio. `appsettings.Development.json` está en `.gitignore`, y en producción los valores se cargan desde los Application Settings del App Service. En App Service Linux el separador de secciones es doble guion bajo: `JwtSettings__Key`, `Encryption__Key`, `MercadoPago__ClientSecret`, `Storage__AzureBlob__ConnectionString`.

CORS está configurado por lista blanca de orígenes en `Program.cs`, con `AllowCredentials` porque SignalR lo requiere.

---

## Estado actual

**Funcionando en producción**

- Autenticación JWT y registro de negocios con clave de alta
- Catálogo: categorías ordenables, productos, adicionales
- Variantes con hasta 2 dimensiones y generación por combinatoria
- Stock por producto o por variante, con auditoría de movimientos, alertas de stock bajo y auto-deshabilitado al agotarse
- Descuentos automáticos por producto, categoría o pedido completo, con vigencia
- Cupones con límite de usos, pedido mínimo y liberación al cancelar
- Pedidos: creación pública, gestión completa, comanda, ticket, comentarios internos
- Resumen de pedido formateado para WhatsApp
- Notificaciones en tiempo real por SignalR
- Subida de imágenes con conversión a WebP, sobre Blob Storage o disco
- Dashboard de ventas con comparación entre períodos
- Mercado Pago: OAuth por negocio, preferencias de pago y webhooks con validación de firma

**Pendiente**

- **Filtros globales de tenant.** Activar `HasQueryFilter` con un `ITenantProvider` inyectado, para que el aislamiento no dependa de filtrar a mano en cada consulta. Es la prioridad.
- **Unificar la generación de slugs.** Hoy hay dos implementaciones que no coinciden: el menú público resuelve el slug en SQL con `ToLower().Replace(" ", "-")`, sin quitar acentos, mientras que la creación de pedidos usa un `Slugify()` en memoria que sí los normaliza. Para un negocio con acentos en el nombre, el slug que sirve el menú no es el mismo que acepta el endpoint de pedidos. Además el slug se deriva del nombre en cada request en lugar de persistirse, así que renombrar el negocio rompe los links públicos.
- **Persistir el costo de envío.** No existe como columna: va sumado dentro de `Pedido.Total` y se reconstruye por resta en el detalle, el ticket y el resumen de WhatsApp, con fórmulas que no son idénticas entre sí.
- **Renovación de tokens de Mercado Pago.** El `RefreshToken` se guarda cifrado y se registra su vencimiento, pero no hay flujo de renovación: cuando expira, el diagnóstico lo informa y el negocio tiene que reconectar a mano.
- **Resolución del pago en el webhook.** Para asociar un `paymentId` a un pedido se pide un token de aplicación por `client_credentials`. Funciona, pero es un rodeo: lo correcto es resolver el tenant desde el propio payload y usar su token.
- **Pagos rechazados.** Un pago en estado `rejected` deja el pedido en `Pendiente`. Fue una decisión deliberada para no cancelar pedidos que el cliente puede reintentar, pero falta el estado intermedio que lo refleje.
- **Estado del OAuth entre reinicios.** El `state` se guarda en `IMemoryCache`: si el proceso reinicia en medio del flujo, o si se escala a más de una instancia, el callback falla. Corresponde moverlo a almacenamiento distribuido.
- **Actualización masiva de precios.** Las tablas `PreviewActualizacionPrecios` y su detalle existen y están migradas, pero no hay endpoints ni lógica: solo está el esquema.
- **Tests.** No hay proyecto de tests. Es la deuda más grande del repositorio.

---

## Autor

Francisco Bover — [github.com/FranBover](https://github.com/FranBover)
