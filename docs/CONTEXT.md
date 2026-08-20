# Vinto — Backend (Vinto.Api) · Documentación técnica interna

> Documentación interna, más profunda que el README. Generada leyendo el código real del repositorio y verificada contra el estado actual de `main`.
> Donde algo no pudo confirmarse con certeza se marca **(verificar)**.
> **No** se incluyen valores de secretos: solo se referencian por nombre de configuración.

Última actualización: 2026-08-20 · Verificado contra el commit `cc8ed06`.

---

## 1. Qué es Vinto

SaaS multi-tenant de tienda online para negocios chicos. Cada negocio (tenant) tiene su catálogo, su panel de administración y su tienda pública.

Nació para un local de comida que tomaba todos los pedidos por teléfono. El dominio se generalizó después: hoy modela productos, variantes, stock, cupones y pedidos sin supuestos sobre el rubro. Quedan restos del origen en la nomenclatura —`Administrador.NombreLocal`, la ruta pública `/locales/{slug}`, el endpoint `comanda`— que no se renombraron para no romper el frontend ni forzar migraciones.

Características que se desprenden del código:

- **Tienda pública por negocio**, accesible por *slug* derivado del nombre (`GET /api/public/locales/{slug}/menu`). Sin login del comprador.
- **Pedidos sin registro**: el comprador arma el pedido contra el endpoint público. Se genera un código de seguimiento.
- **Resumen para WhatsApp**: al crear el pedido el backend devuelve `ResumenWhatsApp`, texto formateado en es-AR listo para enviar. Regenerable desde el panel.
- **Panel privado**: catálogo, variantes, stock, descuentos, cupones, pedidos, imágenes, datos del negocio, reportes y conexión con Mercado Pago. Protegido por JWT.
- **Pagos con Mercado Pago** vía OAuth (cada negocio conecta su propia cuenta), preferencias de Checkout Pro y webhook con firma validada.
- **Tiempo real** con SignalR (`/hubs/pedidos`): notifica nuevos pedidos y pagos confirmados al panel.

**Multi-tenant:**
- En lo **público**, el tenant se resuelve por **slug** (derivado de `Administrador.NombreLocal`).
- En lo **privado**, por **`adminId`** (claim del JWT). Cada entidad de negocio cuelga de `AdministradorId`.

---

## 2. Stack y arquitectura

**Stack** (`Vinto.Api.csproj`):

- ASP.NET Core **.NET 9** (Web API)
- **EF Core 9** + `Microsoft.EntityFrameworkCore.SqlServer` 9.0.4
- **JWT** (`Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.4)
- **SignalR** (incluido en el framework)
- **MercadoPago SDK** 2.12.1
- **Azure.Storage.Blobs** 12.29.1
- **SixLabors.ImageSharp** 3.1.12
- **Swashbuckle** 6.6.2 (solo en Development)
- Hashing de password: `PasswordHasher<Administrador>` (ASP.NET Core Identity)

**Solución/proyecto:** `Eat_Experience.sln` en la raíz del repositorio; el proyecto `Vinto.Api` vive en el subdirectorio `Eat_Experience/`. La carpeta conserva el nombre viejo mientras que el `.csproj` y el namespace raíz ya son `Vinto.Api`. Por eso el pipeline referencia `**/Vinto.Api.csproj` y arranca con `dotnet Vinto.Api.dll`.

**Capas:**

```
Controllers/   → HTTP, extracción de adminId del JWT, validación de entrada, mapeo a DTO
Services/      → Lógica de negocio (Interfaces/ + Implementaciones/)
Repositories/  → Acceso a datos sobre AppDbContext (Interfaces/ + Implementaciones/)
DTOs/          → Contratos de request/response
Models/        → Entidades EF
Data/          → AppDbContext: relaciones, índices, precisión, defaults
Helpers/       → EncryptionHelper (AES-GCM), MercadoPagoSignatureValidator, ValidacionException
Storage/       → IStorageProvider + LocalStorageProvider + AzureBlobStorageProvider
Hubs/          → PedidosHub (SignalR)
Migrations/    → 22 migraciones EF
```

> **Nota sobre la capa de repos:** su uso es parcial y deliberado. Varios controllers y services usan `AppDbContext` directamente cuando la consulta necesita múltiples `Include` (`PublicController.GetMenu` es el caso extremo, con cinco niveles de `ThenInclude`). Conviven ambos estilos; no hay convención única.

**Multi-tenant en la práctica:**

- Los filtros globales (`HasQueryFilter`) están **preparados pero comentados** en `AppDbContext` (líneas ~263-267), porque activarlos requiere inyectar un `ITenantProvider` en el constructor del contexto. El aislamiento se hace **manualmente** filtrando por `AdministradorId` en cada consulta. **Riesgo:** una consulta que olvide el filtro es una fuga entre tenants.
- Patrón en controllers privados: `TryGetAdminId(out int adminId)` lee el claim `adminId`; luego cada operación valida pertenencia de la entidad y devuelve `Forbid()` si no corresponde.
- La unicidad es por tenant: índices compuestos `(AdministradorId, Nombre)` en `Categoria` y `Producto`, `(AdministradorId, Codigo)` en `Cupon`.

**Pipeline (`Program.cs`), en orden de registro:**

JWT (con handler de `access_token` por query string para `/hubs`) → DbContext SQL Server (command timeout 180s) → 13 repositorios → 17 servicios → MemoryCache → HttpClient → `EncryptionHelper` (singleton) → `MercadoPagoSignatureValidator` (singleton) → `IStorageProvider` condicional → `ImagenService` → SignalR → Controllers + Swagger → CORS `AllowFrontend`.

Orden del middleware en la request:

1. **Middleware global de excepciones** (inline, lo más arriba posible)
2. Swagger (solo Development)
3. `UseHttpsRedirection`
4. `UseStaticFiles` + static files de `/uploads` desde `ContentRootPath/uploads`
5. `UseCors("AllowFrontend")`
6. `UseAuthentication` → `UseAuthorization`
7. `MapHub<PedidosHub>("/hubs/pedidos")` + `MapControllers`
8. Seed de admin, **condicionado a `IsDevelopment()`**

**Detalle del middleware de excepciones:** está por encima de `UseCors` a propósito y **no** llama a `Response.Clear()`, para no borrar los headers CORS que ya se aplicaron aguas abajo. Sin eso, un 500 llegaba al navegador sin headers CORS y se reportaba como error de CORS, enmascarando la causa real. El cuerpo está gateado: en producción solo `{ error: "Error interno del servidor" }`; fuera de producción incluye `error`, `detalle` y `tipo`. La excepción completa siempre se loguea con `LogError`.

**CORS `AllowFrontend`** — orígenes permitidos:
- `http://localhost:5173`
- `https://vinto-frontend-dev-ffbbb4e2fzcfd5h9.centralus-01.azurewebsites.net`
- `https://purple-dune-08cbe830f.7.azurestaticapps.net`
- `https://vintoapp.com`
- `https://www.vintoapp.com`

Con `AllowAnyHeader`, `AllowAnyMethod` y `AllowCredentials` (requerido por SignalR).

**Swagger** declara dos security schemes: `Bearer` (JWT) y `X-Admin-Key` (registro de negocios).

---

## 3. Autenticación y autorización

**Login** (`POST /api/Auth/login`): busca por email, verifica con `PasswordHasher.VerifyHashedPassword`, rechaza si `EsActivo == false`, y emite el token.

**Claims del JWT** (`AuthController.GenerateToken`):
- `sub` → email del administrador
- `adminId` → id del tenant (**es el que sostiene todo el aislamiento**)
- `jti` → GUID aleatorio

Firma HMAC-SHA256 con `JwtSettings:Key`. Expiración según `JwtSettings:ExpirationInMinutes`.

**Validación** (`Program.cs`): valida issuer, audience, lifetime y firma. Todo activado.

**Registro** (`POST /api/Auth/register`): `[AllowAnonymous]`, protegido por el header `X-Admin-Key` comparado contra `AdminRegistroKey`. Verifica que el email sea único.

**Autorización por tenant:** no hay roles. El único eje es la pertenencia al tenant. Cada endpoint privado extrae `adminId` del token y valida que el recurso le pertenezca.

> No hay revocación de tokens: el `jti` se emite pero no se persiste ni se chequea contra una lista. Un token robado es válido hasta que expira. Mitigación actual: expiración corta. **(verificar si vale la pena una blacklist)**

---

## 4. Modelo de datos

Entidades reales de `Models/` y `AppDbContext`. `*` = nullable.

### Administrador (tenant)

Tabla central.
- `Id`, `Nombre`, `Email` (**índice único**), `PasswordHash`
- Negocio: `NombreLocal`, `Direccion`, `Telefono`, `LinkWhatsapp*`, `LogoUrl*`, `Horarios*`, `UbicacionUrl*`
- `EsActivo`, `FechaRegistro` (default `GETUTCDATE()`), `UltimoAcceso*`, `PlanSuscripcion*`, `DominioPersonalizado*`
- Transferencia: `AliasTransferencia*`, `TitularCuenta*`
- Envío: `ZonaEnvio` (`"Ciudad"` | `"Nacional"`, default `"Nacional"`), `CostoEnvio*`
- Stock: `StockBajoAlerta*` (default 5), `AutoDeshabilitarSinStock` (default false)
- Mercado Pago: `MercadoPagoUserId*`, `MercadoPagoAccessToken*` (**cifrado**), `MercadoPagoRefreshToken*` (**cifrado**), `MercadoPagoPublicKey*`, `MercadoPagoTokenExpiresAt*`, `MercadoPagoConectado`

Relaciones 1:N, todas `Restrict` salvo `Imagen` (Cascade): Categoria, Producto, Pedido, Descuento, Cupon, MovimientoStock, Imagen, ComentarioPedido.

### Catálogo

**Categoria** — `Id`, `Nombre`, `Orden` (default 0), `AdministradorId`. Índice único `(AdministradorId, Nombre)`.

**Producto** — `Id`, `Nombre`, `Descripcion*`, `Precio` (18,2), `ImagenUrl`, `Disponible` (default true), `CategoriaId`, `AdministradorId`, `TieneVariantes` (default false), `Stock*` (stock simple cuando no tiene variantes). Colecciones: `Extras`, `TiposVariante`, `Variantes`. Índice único `(AdministradorId, Nombre)`.

**ProductoExtra** — `Id`, `Nombre`, `PrecioAdicional` (18,2), `ProductoId` (Restrict).

### Variantes (dos niveles)

**TipoVariante** — `Id`, `ProductoId` (Cascade), `Nombre`, `Orden`. Máximo 2 por producto, validado en el controller.

**OpcionVariante** — `Id`, `TipoVarianteId` (Cascade), `Valor`, `Orden`.

**VarianteProducto** — combinación concreta: `Id`, `ProductoId` (Cascade), `Opcion1Id` (Restrict), `Opcion2Id*` (Restrict), `Precio` (18,2), `Stock*`, `Disponible`, `Sku*`.

> El diseño separa la **definición** de las dimensiones de las **combinaciones** concretas. Permite que cada combinación tenga precio y stock propios sin duplicar la definición.

### Ventas

**Pedido**
- `Id`, `AdministradorId` (Restrict), `Fecha` (default `GETUTCDATE()`), `Estado` (default `"Pendiente"`), `CodigoSeguimiento*`
- Cliente: `NombreCliente`, `TelefonoCliente`
- Pago/entrega: `FormaPago`, `FormaEntrega`, `MontoPagoEfectivo*`, `DireccionCliente*`, `ReferenciaDireccion*`, `UbicacionUrl*`
- Totales: `Total` (18,2), `SubtotalSinDescuentos`, `MontoDescuentoProductos`, `MontoDescuentoCupon` (todos default 0)
- Cupón: `CuponId*` (Restrict), `CodigoCupon*`
- Mercado Pago: `MercadoPagoPreferenceId*`, `MercadoPagoPaymentId*`, `MercadoPagoStatus*`, `MercadoPagoStatusDetail*`, `MercadoPagoFechaPago*`, `MercadoPagoCollectionId*`
- Colección `Detalles` (Cascade). Índice `(AdministradorId, Fecha)`

Estados validados en `PedidosController.PatchEstado`: `Pendiente`, `Confirmado`, `EnPreparacion`, `Listo`, `Entregado`, `Cancelado`.

**DetallePedido** — `Id`, `PedidoId` (Cascade), `ProductoId` (Restrict), `Cantidad`, `PrecioUnitario` (18,2; guarda el precio **ya con el descuento de línea aplicado**), `VarianteProductoId*` (Restrict). Colección `ProductosExtra`.

**DetallePedidoExtra** — `Id`, `DetallePedidoId` (Cascade), `ProductoExtraId` (Restrict). Índice único `(DetallePedidoId, ProductoExtraId)`: evita repetir el mismo adicional en la misma línea.

**ComentarioPedido** — `Id`, `PedidoId` (Cascade), `Texto` (máx 500), `FechaCreacion`, `AdministradorId` (Restrict). Notas internas.

### Promociones

**Descuento** — `Id`, `AdministradorId` (Restrict), `Nombre`, `Tipo` (`"Porcentaje"` | otro = monto fijo), `Valor` (18,2), `ProductoId*` (SetNull), `CategoriaId*` (SetNull), `AplicaAPedidoCompleto`, `FechaInicio*`, `FechaFin*`, `Activo`, `FechaCreacion`. Índice `(AdministradorId, Activo)`.

**Cupon** — `Id`, `AdministradorId` (Restrict), `Codigo` (máx 30), `Tipo` (`"Porcentaje"` | `"MontoFijo"`), `Valor` (18,2), `FechaVencimiento*`, `LimiteUsos*`, `UsosActuales` (default 0), `PedidoMinimo*`, `Activo`, `FechaCreacion`. Índice único `(AdministradorId, Codigo)`.

**UsoCupon** — `Id`, `CuponId` (Restrict), `PedidoId` (Cascade), `MontoDescontado` (18,2), `FechaUso`, `Liberado`, `FechaLiberacion*`. Permite liberar y re-aplicar el cupón cuando un pedido se cancela o reactiva.

**DetallePedidoDescuento** — snapshot de auditoría: `Id`, `PedidoId` (Cascade), `DetallePedidoId*` (Restrict), `DescuentoId*` (SetNull), `NombreDescuentoSnapshot`, `TipoDescuento` (`"Producto"`/`"Categoria"`/`"PedidoCompleto"`), `MontoDescontado` (18,2), `FechaCreacion`.

> El snapshot guarda el nombre, no solo la FK: si la regla se edita o se borra, el pedido histórico sigue siendo legible.

### Operaciones

**MovimientoStock** — `Id`, `AdministradorId`, `ProductoId`, `VarianteProductoId*`, `Tipo` (`"entrada"`|`"salida"`|`"ajuste"`), `Cantidad`, `StockAnterior`, `StockNuevo`, `Motivo*`, `FechaCreacion`. Todas las FK `Restrict`. Guarda stock anterior **y** nuevo, así el movimiento se puede auditar sin recalcular la cadena entera.

**Imagen** — `Id`, `AdministradorId` (Cascade), `NombreOriginal` (255), `NombreAlmacenado` (255), `ContentType` (100), `TamanioBytes`, `Url` (500), `Tipo` (`"producto"` | `"logo"` | `"categoria"`), `EntidadId*`, `Orden` (default 0), `FechaCreacion`. Índice `(AdministradorId, Tipo, EntidadId)`.

> El comentario del modelo menciona solo `"producto"`/`"logo"`, pero el código usa además `"categoria"`. El comentario está desactualizado, no el código.

**PagoMercadoPago** — `Id`, `PedidoId` (Restrict), `PaymentId`, `Status`, `StatusDetail*`, `Monto` (18,2), `FechaEvento`, `RawWebhookData*` (JSON crudo), `ProcesadoConExito`. **Índice único `(PaymentId, Status)`** → idempotencia del webhook garantizada por la base.

**PreviewActualizacionPrecios / PreviewActualizacionPreciosItem** — ❌ **sin uso**. Modelos, `DbSet` y migración aplicada, pero ningún controller ni service los referencia. Feature a medio implementar: solo el esquema.

**JwtSettings** — no es entidad; se bindea desde la sección `JwtSettings` de configuración.

### Convenciones del esquema

- **Todo campo monetario es `decimal(18,2)`**, declarado con `HasPrecision`. Nunca punto flotante.
- **Fechas en UTC**, con default `GETUTCDATE()` en: `Administrador.FechaRegistro`, `Pedido.Fecha`, `Imagen.FechaCreacion`, `MovimientoStock.FechaCreacion`, `Descuento/Cupon/UsoCupon/DetallePedidoDescuento.FechaCreacion`. La conversión a horario argentino ocurre solo en `ReporteService`.
- **`DeleteBehavior.Restrict` por defecto.** Cascade solo donde el hijo no tiene sentido sin el padre (detalles de pedido, opciones de variante, imágenes del tenant). Un producto vendido alguna vez no se puede borrar.

---

## 5. Endpoints

Rutas reales tomadas de los controllers. `[controller]` resuelve al nombre sin sufijo.

### Públicos (sin JWT)

| Método | Ruta | Qué hace |
|---|---|---|
| POST | `/api/Auth/register` | Registra un negocio. Requiere header `X-Admin-Key`. |
| POST | `/api/Auth/login` | Login, devuelve `{ token }`. |
| GET | `/api/public/locales/{slug}/menu` | Catálogo público: datos del negocio, categorías con productos disponibles, variantes, extras, imágenes y descuentos a pedido completo. |
| GET | `/api/public/pedidos/{codigoSeguimiento}/estado-pago` | Estado del pedido y del pago, más el resumen. |
| POST | `/api/public/locales/{slug}/pedidos` | Crea pedido. Devuelve `PedidoCreateResponseDTO` con `ResumenWhatsApp` y `CodigoSeguimiento`. Definido en `PedidosController` con ruta absoluta. |
| POST | `/api/public/locales/{slug}/pedidos/{pedidoId}/preferencia-mp` | Crea la preferencia de pago. Body: `{ codigoSeguimiento }`. |
| POST | `/api/public/locales/{slug}/cupones/validar` | Valida cupón contra un subtotal. `[AllowAnonymous]` en `CuponesController`. |
| GET | `/api/MercadoPago/oauth/callback` | Callback OAuth. `[AllowAnonymous]`; la seguridad la da el `state`. Redirige al front admin. |
| POST | `/api/MercadoPago/webhook` | Webhook de pagos. `[AllowAnonymous]`; la seguridad la da la firma HMAC. Responde 200 salvo error grave. |
| POST | `/api/MercadoPago/dev/simular-webhook-aprobado` | **Solo Development**: simula un pago aprobado. |

> Los endpoints públicos de pedidos y cupones viven en controllers con `[Authorize]` a nivel de clase, pero se declaran con ruta absoluta y, en el caso de cupones, con `[AllowAnonymous]` explícito. En `PedidosController` el `[Authorize]` está por acción, no en la clase.

### Privados (JWT `Bearer`)

**Administrador** — `[Authorize]` a nivel de clase

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/Administrador/{id}` | Obtiene el negocio. Valida pertenencia y devuelve un DTO sin secretos. |
| PATCH | `/api/Administrador/{id}/local` | Patch parcial. Verifica `id == adminId` del token. |

> Este controller se redujo deliberadamente. El listado de todos los administradores y el CRUD que recibía la entidad cruda se eliminaron en el commit `1a43c79`.

**Catálogo**

| Método | Ruta | Qué hace |
|---|---|---|
| GET / POST | `/api/Categorias` | Lista (con imagen) / crea. |
| GET / PUT / DELETE | `/api/Categorias/{id}` | Obtiene / actualiza / elimina (borra también sus imágenes). |
| PATCH | `/api/Categorias/reordenar` | Reordena todas las del tenant (`{ orderedIds }`). |
| GET / POST | `/api/Productos` | Lista (con imágenes) / crea. |
| GET / PUT / DELETE | `/api/Productos/{id}` | Obtiene / actualiza / elimina. |
| PATCH | `/api/Productos/{id}/disponibilidad` | Cambia disponibilidad. |
| GET / POST | `/api/ProductoExtra` | Lista / crea. |
| GET / PUT / DELETE | `/api/ProductoExtra/{id}` | Obtiene / actualiza / elimina. |
| GET | `/api/ProductoExtra/por-producto/{productoId}` | Extras de un producto. |

**Variantes y stock**

| Método | Ruta | Qué hace |
|---|---|---|
| GET / POST | `/api/Productos/{productoId}/tipos-variante` | Lista / crea (máx 2). |
| PUT / DELETE | `/api/Productos/{productoId}/tipos-variante/{id}` | Edita / elimina. |
| GET / POST | `/api/tipos-variante/{tipoId}/opciones` | Lista / crea opción. |
| PUT / DELETE | `/api/tipos-variante/{tipoId}/opciones/{id}` | Edita / elimina opción. |
| GET | `/api/Productos/{productoId}/variantes` | Lista combinaciones. |
| POST | `/api/Productos/{productoId}/variantes/generar` | Genera por combinatoria. |
| DELETE | `/api/Productos/{productoId}/variantes` | Elimina todas. |
| PUT / DELETE | `/api/Variantes/{varianteId}` | Edita precio/stock/disponible/sku · elimina (falla si tiene pedidos). |
| GET | `/api/Productos/{productoId}/stock` | Estado + últimos movimientos. |
| POST | `/api/Productos/{productoId}/stock/ajustar` | Fija el stock a un valor. |
| POST | `/api/Productos/{productoId}/stock/agregar` | Repone. |
| GET | `/api/Stock/alertas` | Stock bajo o agotado según `StockBajoAlerta`. |

**Promociones**

| Método | Ruta | Qué hace |
|---|---|---|
| GET / POST | `/api/Descuentos` | Lista (filtro `activo`) / crea. |
| GET / PUT | `/api/Descuentos/{id}` | Obtiene / actualiza. |
| GET / POST | `/api/Cupones` | Lista (filtro `activo`) / crea. |
| GET / PUT | `/api/Cupones/{id}` | Obtiene / actualiza. |
| GET | `/api/Cupones/{id}/metricas` | Métricas de uso. |

> Ni `Descuento` ni `Cupon` tienen DELETE. Se desactivan con el flag `Activo` vía update, para no romper el historial de pedidos que los referencian.

**Pedidos**

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/Pedidos?estado=&desde=&hasta=&formaPago=&formaEntrega=` | Lista con filtros. |
| GET | `/api/Pedidos/{id}` | Detalle con desglose de totales y datos de MP. |
| PATCH | `/api/Pedidos/{id}/estado` | Cambia estado: descuenta o repone stock y gestiona el cupón. |
| GET | `/api/Pedidos/{id}/resumen` | Resumen para WhatsApp. |
| GET | `/api/Pedidos/{id}/comanda` | Vista de preparación. |
| GET | `/api/Pedidos/{id}/ticket` | Ticket con totales y vuelto. |
| GET / POST | `/api/Pedidos/{id}/comentarios` | Lista / agrega nota interna. |

> El `PUT /api/Pedidos/{id}` legado —que reemplazaba la entidad completa sin validar pertenencia ni recalcular totales— se eliminó en el commit `69e75ed`.

**Imágenes, Mercado Pago y reportes**

| Método | Ruta | Qué hace |
|---|---|---|
| POST | `/api/Imagenes/upload` | Multipart: `file`, `tipo`, `entidadId`, `orden`. |
| GET | `/api/Imagenes?tipo=&entidadId=` | Lista por entidad. |
| DELETE | `/api/Imagenes/{id}` | Elimina. |
| GET | `/api/MercadoPago/oauth/url` | URL de autorización. |
| POST | `/api/MercadoPago/desconectar` | Desvincula la cuenta. |
| GET | `/api/MercadoPago/estado` | Estado de conexión. |
| GET | `/api/MercadoPago/diagnostico` | Token expirado, pedidos pendientes con MP. |
| GET | `/api/Reportes/dashboard?periodo=` | Períodos: `hoy`, `semana`, `mes`, `anio`. |

**SignalR:** `/hubs/pedidos`. Eventos servidor → cliente: `NuevoPedido` (desde `PedidoService`), `PagoConfirmado` (desde `MercadoPagoService`, dos puntos de emisión).

---

## 6. Subsistemas

### 6.1 SignalR

`PedidosHub` tiene `[Authorize]` a nivel de clase. En `OnConnectedAsync` **deriva el grupo del claim `adminId` del token** y suscribe la conexión ahí. No expone métodos `JoinGroup`/`LeaveGroup` invocables por el cliente: si los expusiera, cualquier token válido podría suscribirse al grupo de otro tenant y recibir sus pedidos en vivo. SignalR remueve la conexión de sus grupos al desconectarse, así que no hace falta limpieza manual.

El handshake de WebSocket no puede llevar el header `Authorization`. `Program.cs` lo resuelve con `JwtBearerEvents.OnMessageReceived`, que lee el token del query string `access_token` **solo** si el path empieza con `/hubs`.

### 6.2 Cifrado (`EncryptionHelper`)

AES-GCM 256 bits. Clave desde `Encryption:Key`, base64 de exactamente 32 bytes; si no, lanza excepción en el constructor (falla al arrancar, no en runtime).

- Nonce aleatorio de 12 bytes por operación, tag de 16 bytes
- Formato almacenado: `nonce + tag + ciphertext`, en base64
- Se eligió GCM sobre CBC por ser cifrado autenticado: detecta manipulación del ciphertext

Se usa para `MercadoPagoAccessToken` y `MercadoPagoRefreshToken` en la tabla `Administrador`.

> **Consecuencia operativa:** rotar `Encryption:Key` invalida todo lo ya cifrado. Requiere un script de recifrado, o forzar a todos los negocios a reconectar Mercado Pago.

### 6.3 Mercado Pago

**OAuth:** `GetOAuthUrl` genera un `state` aleatorio de 32 bytes en base64url, lo guarda en `IMemoryCache` como `mp_oauth_state:{state}` → `adminId` con TTL, y arma la URL de autorización. `ProcesarCallback` valida el `state` contra el cache, lo **elimina** (uso único), intercambia el `code` por tokens y los persiste cifrados.

**Preferencias:** `CrearPreferenciaPago` descifra el access token del negocio y crea la preferencia con `back_urls` de success/failure/pending que apuntan al frontend. `TruncarStatementDescriptor` normaliza el nombre del negocio a máximo 22 caracteres alfanuméricos para el resumen de la tarjeta.

**Webhook:** valida la firma HMAC del header `x-signature` (formato `ts=...,v1=...`) contra `MercadoPago:WebhookSecret`. La idempotencia la da el índice único `(PaymentId, Status)`, no un chequeo en código. Guarda el payload crudo en `RawWebhookData`. Al confirmar un pago emite `PagoConfirmado` por SignalR.

**Estado de la integración:** funcional, con tres huecos conocidos (ver §8).

### 6.4 Almacenamiento e imágenes

`IStorageProvider` con dos implementaciones. `Program.cs` registra `AzureBlobStorageProvider` si `Storage:Provider == "AzureBlob"` (comparación case-insensitive); cualquier otro valor o su ausencia cae en `LocalStorageProvider`.

- `LocalStorageProvider` escribe en `ContentRootPath/{Storage:Local:BasePath}` (default `uploads`), servido por static files en `/uploads`. **En App Service el disco es efímero:** solo sirve para desarrollo.
- `AzureBlobStorageProvider` requiere `Storage:AzureBlob:ConnectionString` y `:ContainerName`; falla en el constructor si falta alguno. Devuelve la URI del blob.

`ImagenService.UploadAsync` normaliza toda imagen entrante:
1. Valida content type contra lista blanca: `image/jpeg`, `image/png`, `image/webp`, `image/gif`
2. Rechaza si supera 5 MB
3. Redimensiona a 1200px de ancho máximo (`ResizeMode.Max`, solo si excede)
4. Convierte a **WebP** con calidad 85
5. Nombre de archivo: `{GUID}.webp` — nunca se usa el nombre original en disco

El nombre original se guarda solo como metadato en `Imagen.NombreOriginal`.

### 6.5 Descuentos (`DescuentoCalculatorService`)

Orden de aplicación, explícito y determinista:

1. Descuentos de **producto**, ordenados por `FechaCreacion` ascendente
2. Descuentos de **categoría**, sobre el precio ya reducido, mismo criterio de orden
3. Descuentos a **pedido completo**, sobre el subtotal posterior a los ítems

El orden por `FechaCreacion` evita que el resultado dependa del orden en que SQL devuelva las filas. Hay dos *clamps*: el precio unitario nunca baja de 0.01 y el subtotal global no queda negativo. `MontoDescuentoPedidoCompleto` refleja la reducción **real** después del clamp, no la nominal.

`Descuento.Tipo` se trata como porcentaje si es exactamente `"Porcentaje"`; cualquier otro valor se interpreta como monto fijo. `Cupon.Tipo` en cambio usa `"Porcentaje"`/`"MontoFijo"` explícitos. Los dos criterios no son idénticos. **(verificar qué valores escribe exactamente `DescuentoService`)**

### 6.6 Stock (`StockService`)

Tres operaciones, todas registrando un `MovimientoStock` con stock anterior y nuevo:

- `DescontarStock` — al confirmar el pedido
- `ReponerStock` — al cancelar
- `AjustarStock` — fija un valor absoluto

Funciona tanto sobre stock simple del producto como sobre el de una variante (`varianteId` nullable). Si `AutoDeshabilitarSinStock` está activo, al llegar a 0 el producto se marca no disponible.

### 6.7 Reportes (`ReporteService`)

Único reporte: dashboard de ventas. Períodos aceptados: `hoy`, `semana`, `mes`, `anio`; cualquier otro valor lanza excepción con el listado válido.

Es el único subsistema con manejo explícito de zona horaria: resuelve `TimeZoneInfo` de Argentina una vez en un campo estático, convierte UTC → Argentina para calcular los rangos, y de vuelta a UTC para consultar. Sin esto, "ventas de hoy" arrancaría a las 21:00 del día anterior.

---

## 7. Configuración y entorno

### Claves requeridas

| Clave | Requerida | Notas |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Sí | SQL Server. Command timeout 180s configurado en código. |
| `JwtSettings:Key` | Sí | HMAC-SHA256. Falla al arrancar si falta. |
| `JwtSettings:Issuer` / `:Audience` | Sí | Validados en cada request. |
| `JwtSettings:ExpirationInMinutes` | Sí | — |
| `AdminRegistroKey` | Sí | Habilita `POST /api/Auth/register`. |
| `Encryption:Key` | Sí | 32 bytes en base64. Falla al construir el helper si no. |
| `MercadoPago:ClientId` / `:ClientSecret` | Para pagos | — |
| `MercadoPago:WebhookSecret` | Para pagos | Validación de firma. |
| `MercadoPago:RedirectUri` / `:BackendUrl` | Para pagos | Deben ser URLs públicas. |
| `MercadoPago:AuthBaseUrl` / `:ApiBaseUrl` | Para pagos | — |
| `MercadoPago:FrontendAdminUrl` / `:FrontendClientUrl` | Para pagos | Destino de las redirecciones. |
| `Storage:Provider` | No | `"AzureBlob"` o cualquier otro valor → local. |
| `Storage:Local:BasePath` | No | Default `uploads`. |
| `Storage:AzureBlob:ConnectionString` / `:ContainerName` | Si `Provider=AzureBlob` | Falla al construir el provider si faltan. |

> **`appsettings.Example.json` está incompleto.** Solo cubre `ConnectionStrings`, `JwtSettings`, `AdminRegistroKey`, `Logging` y `AllowedHosts`. Le faltan `Encryption`, `MercadoPago` y `Storage`. Alguien que siga solo ese archivo no logra levantar el proyecto con pagos ni con Blob Storage. **Pendiente de completar.**

### Perfiles locales

`launchSettings.json`: HTTP en `http://localhost:5202`, HTTPS en `https://localhost:7288;http://localhost:5202`, más un perfil IIS Express en `:62159`. `ASPNETCORE_ENVIRONMENT=Development`, `launchUrl` = `swagger`.

### Migraciones

22 migraciones, de `20250512004358_InitialCreate` a `20260611235002_AddOrdenToCategoria`. La evolución del esquema se lee como la del producto: pedidos y detalles → multi-tenancy (`20250915224651_multitenant-v1`) → dirección y envío → comentarios → imágenes → variantes y stock → descuentos y cupones → Mercado Pago en tres pasos → orden de categorías.

**No hay `Database.Migrate()` en `Program.cs`**: las migraciones se aplican manualmente con `dotnet ef database update`.

### Seed

Bloque al final de `Program.cs`, **condicionado a `IsDevelopment()`**. Si la tabla `Administradores` está vacía crea un admin de prueba con credenciales fijas y `NombreLocal` vacío (por lo tanto, slug vacío: ningún endpoint público lo resuelve). Para crear negocios reales, usar `POST /api/Auth/register`.

---

## 8. Deuda técnica y bugs conocidos

Ordenado por impacto.

1. **Filtros multi-tenant manuales.** `HasQueryFilter` comentado en `AppDbContext`. Toda consulta debe filtrar `AdministradorId` a mano; un olvido es fuga entre tenants. **Prioridad máxima.**

2. **Dos algoritmos de slug incompatibles.** Bug real, reproducible:
   - `PublicController.GetMenu` y `MercadoPagoService.CrearPreferenciaPago` resuelven en **SQL**: `NombreLocal.ToLower().Replace(" ", "-")`, **sin** quitar acentos.
   - `PedidoService.CrearPublicoPorSlug` y `CuponService` usan `Slugify()` en **memoria**, que sí normaliza acentos (á→a, ñ→n) y colapsa espacios y guiones bajos.
   - Para un negocio llamado "Café León": el menú se sirve en `café-león` pero crear el pedido exige `cafe-leon`. El comprador ve la carta y no puede comprar.

3. **Slug derivado, no persistido.** Se calcula desde `NombreLocal` en cada request. Renombrar el negocio rompe todos los links públicos existentes. Además dos negocios cuyos nombres difieran solo en acentos o espacios pueden colisionar. Corresponde persistir una columna `Slug` única e indexada.

4. **Pedido público puede devolver 500 con datos incompletos.** `PedidoPublicCreateRequestDTO.NombreCliente` y `.TelefonoCliente` son `string?`, pero `Pedido.NombreCliente`/`.TelefonoCliente` son `[Required]` (NOT NULL en la base). `CrearPublicoPorSlug` valida cantidad, producto, variante, forma de pago y dirección de delivery, **pero no valida estos dos campos**. Si llegan `null`, `SaveChanges` falla con violación de NOT NULL y sale como 500 genérico. *(Verificado: sigue presente.)*

5. **`CostoEnvio` no se persiste como columna.** Va sumado dentro de `Pedido.Total` y se reconstruye por resta en tres lugares, con fórmulas que no coinciden:
   - `PedidosController.Get(id)`: `Total - subtotalConDescuentos - extras`
   - `PedidoService.GetTicketAsync`: `Total - subtotal + MontoDescuentoCupon`
   - `GenerarResumenWhatsApp`: `Total - (subtotalBruto - descProductos - descCupón)`

   Frágil ante cualquier cambio en el cálculo de totales.

6. **Mercado Pago: sin renovación de token.** `MercadoPagoRefreshToken` se guarda cifrado y `MercadoPagoTokenExpiresAt` se registra, pero **no existe ningún flujo que use el refresh token**. Al expirar, `GET /api/MercadoPago/diagnostico` lo informa y el negocio debe reconectar a mano.

7. **Webhook resuelve el pago con token de aplicación.** Para asociar un `paymentId` a un pedido, `ProcesarWebhookPago` pide un token por `client_credentials` y consulta `/v1/payments/{id}`. El comentario en el código reconoce que es la "opción C" y que es discutible. Lo correcto sería resolver el tenant desde el payload y usar su propio token.

8. **Pagos rechazados dejan el pedido en `Pendiente`.** Decisión deliberada y comentada, para no cancelar pedidos que el comprador puede reintentar. Falta un estado que refleje el intento fallido.

9. **`state` del OAuth en `IMemoryCache`.** No sobrevive a reinicios del proceso ni funciona con más de una instancia: el callback falla porque el `state` no está en el cache de la instancia que atiende. Corresponde almacenamiento distribuido.

10. **Capa de repositorios parcial.** Repos y acceso directo a `AppDbContext` conviven sin convención.

11. **Actualización masiva de precios sin implementar.** `PreviewActualizacionPrecios` y `PreviewActualizacionPreciosItem` tienen modelos, `DbSet`, configuración en `OnModelCreating` y migración aplicada, pero **ningún** controller ni service los usa.

12. **Naming inconsistente entre `Descuento` y `Cupon`.** Ver §6.5.

13. **`[Required, MaxLength(30)]` sobre `DateTime Fecha`** en `Pedido.cs:20`. `MaxLength` no tiene efecto sobre un `DateTime`. Anotación inútil, inofensiva. *(Verificado: sigue presente.)*

14. **Sin revocación de tokens.** El `jti` se emite pero no se persiste ni se valida.

15. **Sin tests.** No hay proyecto de tests en la solución. La deuda más grande.

16. **`appsettings.Example.json` incompleto.** Ver §7.

---

## 9. Deploy

**Azure Pipelines**, `azure-pipelines.yml` en la raíz. Ya **no** hay copia duplicada dentro del proyecto: se unificó en el commit `c9337cb`.

```
trigger: main
pool: ubuntu-latest
steps:
  1. UseDotNet@2     → SDK 9.x
  2. DotNetCoreCLI@2 → publish **/Vinto.Api.csproj, Release, zipAfterPublish
  3. AzureWebApp@1   → suscripción 'Azure-Vinto'
                       app 'vinto-carripollo-api-dev-linux' (webAppLinux)
                       runtime DOTNETCORE|9.0
                       startUpCommand 'dotnet Vinto.Api.dll'
```

Se migró de `windows-2022` + `VSBuild` + paquete WebDeploy a Linux + `dotnet publish`: más rápido, agentes más baratos, sin dependencia de MSBuild.

**Configuración en producción:** Application Settings del App Service. En **Linux** el separador de secciones es doble guion bajo, no dos puntos: `JwtSettings__Key`, `Encryption__Key`, `MercadoPago__ClientSecret`, `Storage__AzureBlob__ConnectionString`.

**Sin GitHub Actions.** No existe `.github/workflows`.

---

## 10. Higiene del repositorio

**Secretos:** ningún archivo con credenciales reales está trackeado actualmente. `appsettings.json` y `appsettings.Development.json` están cubiertos por `Eat_Experience/.gitignore` (líneas 486-487). `appsettings.Example.json` sí está versionado, pero solo con placeholders.

> **Historial:** hubo una fuga de credenciales por `build_temp/` (output de compilación commiteado por error) entre `b478f78` y `e990fd5`. Los archivos se eliminaron del árbol pero **siguen siendo recuperables desde el historial**, y el repositorio es público. Existe una auditoría completa fuera del repositorio con el detalle y el plan de rotación. **Rotar las credenciales es lo que revoca el acceso; borrar el archivo no.**

**Archivos trackeados que no deberían estarlo:** 21 archivos bajo `.vs/` (incluidos índices de Copilot, `.suo`, `slnx.sqlite`) y `obj/Eat_Experience.csproj.EntityFrameworkCore.targets`. El `.gitignore` los cubre, pero git ignora esas reglas para archivos que ya están en el índice: hay que sacarlos con `git rm --cached`. No contienen secretos (verificado); son ruido que genera conflictos entre máquinas.

**`.gitignore`:** hay dos, uno en la raíz y otro en `Eat_Experience/`. Entre ambos la cobertura es correcta y completa para `appsettings` reales, `.vs/`, `obj/`, `bin/`, `uploads/` y `build_temp/`.
