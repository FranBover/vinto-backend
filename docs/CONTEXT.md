# Vinto — Backend (Vinto.Api) · Documentación técnica interna

> Documentación interna, más profunda que el README. Generada leyendo el código real del repositorio y verificada contra el estado actual de `main`.
> Donde algo no pudo confirmarse con certeza se marca **(verificar)**.
> **No** se incluyen valores de secretos: solo se referencian por nombre de configuración.

Última actualización: 2026-10-07 · Verificado contra el commit `2d837bc` (`main`, árbol limpio).

---

## 1. Qué es Vinto

SaaS multi-tenant de tienda online para negocios chicos. Cada negocio (tenant) tiene su catálogo, su panel de administración y su tienda pública.

Nació para un local de comida que tomaba todos los pedidos por teléfono. El dominio se generalizó después: hoy modela productos, variantes, stock, cupones y pedidos sin supuestos sobre el rubro. Quedan restos del origen en la nomenclatura —`Administrador.NombreLocal`, la ruta pública `/locales/{slug}`, el endpoint `comanda`— que no se renombraron para no romper el frontend ni forzar migraciones.

Características que se desprenden del código:

- **Tienda pública por negocio**, accesible por *slug* persistido (`Administrador.SlugLocal`, ver §6.8) en `GET /api/public/locales/{slug}/menu`. Sin login del comprador. El menú se cachea 60 s (§6.10).
- **Pedidos sin registro**: el comprador arma el pedido contra el endpoint público. Se genera un código de seguimiento.
- **Resumen para WhatsApp**: al crear el pedido el backend devuelve `ResumenWhatsApp`, texto formateado en es-AR listo para enviar. Regenerable desde el panel.
- **Panel privado**: catálogo, variantes, stock, descuentos, cupones, pedidos, imágenes, datos del negocio, reportes y conexión con Mercado Pago. Protegido por JWT.
- **Pagos con Mercado Pago** vía OAuth (cada negocio conecta su propia cuenta), preferencias de Checkout Pro y webhook con firma validada.
- **Tiempo real** con SignalR (`/hubs/pedidos`): notifica nuevos pedidos y pagos confirmados al panel.

**Multi-tenant:**
- En lo **público**, el tenant se resuelve por **slug** (`Administrador.SlugLocal`, columna única, independiente de `NombreLocal`) vía `IAdministradorRepository.ObtenerActivoPorSlugAsync`. Un local con `EsActivo = false` no resuelve.
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
Data/          → AppDbContext (relaciones, índices, precisión, defaults) + MenuCacheInvalidationInterceptor
Helpers/       → EncryptionHelper (AES-GCM), MercadoPagoSignatureValidator, SlugHelper, ValidacionException
Storage/       → IStorageProvider + LocalStorageProvider + AzureBlobStorageProvider
Hubs/          → PedidosHub (SignalR)
Migrations/    → 24 migraciones EF
```

> **Nota sobre la capa de repos:** su uso es parcial y deliberado. Varios controllers y services usan `AppDbContext` directamente cuando la consulta necesita múltiples `Include` (`PublicController.GetMenu` es el caso extremo, con cinco niveles de `ThenInclude`). Conviven ambos estilos; no hay convención única.

**Multi-tenant en la práctica:**

- Los filtros globales (`HasQueryFilter`) están **preparados pero comentados** en `AppDbContext` (líneas ~263-267), porque activarlos requiere inyectar un `ITenantProvider` en el constructor del contexto. El aislamiento se hace **manualmente** filtrando por `AdministradorId` en cada consulta. **Riesgo:** una consulta que olvide el filtro es una fuga entre tenants.
- Patrón en controllers privados: el claim `adminId` se lee en cada controller. Los de catálogo/promociones/imágenes/reportes tienen un `TryGetAdminId(out int)` **privado propio** (no hay uno compartido); `PedidosController`, `StockController` y `AdministradorController` lo leen inline (`ObtenerAdminId()` en Stock). Si falta el claim, Pedidos y Stock devuelven `Unauthorized()`; Administrador devuelve `Forbid()`. Luego cada operación valida pertenencia de la entidad.
- La unicidad es por tenant: índices compuestos `(AdministradorId, Nombre)` en `Categoria` y `Producto`, `(AdministradorId, Codigo)` en `Cupon`. El slug es único **global** (§6.8).

**Registro de servicios (`Program.cs`), en orden:**

JWT (con handler de `access_token` por query string para `/hubs`) → `IMenuPublicoCache` (singleton) + `MenuCacheInvalidationInterceptor` (scoped) → DbContext SQL Server (command timeout 180s, con el interceptor) → 13 repositorios → 15 servicios de dominio → `Configure<ForwardedHeadersOptions>` → MemoryCache → HttpClient → `EncryptionHelper` y `MercadoPagoSignatureValidator` (singletons) → `IStorageProvider` condicional → `ImagenService` → SignalR → Controllers + Swagger → CORS `AllowFrontend` → `AddRateLimiter`.

> `IDetallePedidoRepository` está registrado dos veces (inofensivo) e `IDetallePedidoService` no está registrado (nadie lo inyecta).

**Orden del middleware en la request:**

1. `UseForwardedHeaders` — **condicional**: solo si el host no lo registró ya (§6.9)
2. **Middleware global de excepciones** (inline)
3. Swagger (solo Development)
4. `UseHttpsRedirection`
5. `UseStaticFiles` + static files de `/uploads` desde `ContentRootPath/uploads`
6. `UseCors("AllowFrontend")`
7. `UseRateLimiter` — después de CORS (el 429 lleva headers CORS) y de ForwardedHeaders (IP real)
8. `UseAuthentication` → `UseAuthorization`
9. `MapHub<PedidosHub>("/hubs/pedidos")` + `MapControllers`
10. Seed de admin, **condicionado a `IsDevelopment()`**

**Detalle del middleware de excepciones:** está por encima de `UseCors` a propósito y **no** llama a `Response.Clear()`, para no borrar los headers CORS que ya se aplicaron aguas abajo. Sin eso, un 500 llegaba al navegador sin headers CORS y se reportaba como error de CORS, enmascarando la causa real. El cuerpo está gateado: en producción solo `{ error: "Error interno del servidor" }`; fuera de producción incluye `error`, `detalle` y `tipo`. La excepción completa siempre se loguea con `LogError`.

> **Este middleware NO traduce `ValidacionException`.** Trata toda excepción como 500. La traducción a 400/404 la hace cada controller con su propio `catch (ValidacionException)` (Cupones, Descuentos, MercadoPago parcialmente, Pedidos público, Administrador.PatchLocal). Donde el controller no la atrapa, el cliente recibe 500. Casos hoy sin catch: `GET /api/Pedidos` con `page`/`pageSize` inválidos, y `GET estado` y `POST desconectar` de MercadoPago cuando el administrador no existe. Además `ValidacionException.StatusCode` casi siempre se ignora: los catch devuelven 400 (solo `DescuentosController` distingue el 404).

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

**Registro** (`POST /api/Auth/register`): `[AllowAnonymous]`, protegido por el header `X-Admin-Key` comparado contra `AdminRegistroKey`. Verifica que el email sea único y genera el `SlugLocal` (§6.8). Login y registro tienen rate limit por IP (§6.10).

**Autorización por tenant:** no hay roles. El único eje es la pertenencia al tenant. Cada endpoint privado extrae `adminId` del token y valida que el recurso le pertenezca.

> No hay revocación de tokens: el `jti` se emite pero no se persiste ni se chequea contra una lista. Un token robado es válido hasta que expira. Mitigación actual: expiración corta. **(verificar si vale la pena una blacklist)**

---

## 4. Modelo de datos

Entidades reales de `Models/` y `AppDbContext`. `*` = nullable.

### Administrador (tenant)

Tabla central.
- `Id`, `Nombre`, `Email` (**índice único**), `PasswordHash`
- Negocio: `NombreLocal`, `SlugLocal` (`nvarchar(60)` NOT NULL, **índice único** `IX_Administradores_SlugLocal`; no cambia al renombrar `NombreLocal`), `Direccion`, `Telefono`, `LinkWhatsapp*`, `LogoUrl*`, `Horarios*`, `UbicacionUrl*`
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
- Colección `Detalles` (Cascade). Índices `(AdministradorId, Fecha)` (sostiene el listado paginado) y `CodigoSeguimiento` (para `estado-pago`; no es único)
- `FormaEntrega` solo se crea como `Delivery` o `Retira` desde el endpoint público (whitelist, §6.11)

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

La columna *Rate limit* es la política de §6.9; "—" = sin límite.

| Método | Ruta | Rate limit | Qué hace |
|---|---|---|---|
| POST | `/api/Auth/register` | `register` | Registra un negocio. Requiere header `X-Admin-Key`. Genera el `SlugLocal` desde `NombreLocal`. |
| POST | `/api/Auth/login` | `login` | Login, devuelve `{ token }`. |
| GET | `/api/public/locales/{slug}/menu` | — | Catálogo público (cacheado 60 s, header `X-Cache: HIT/MISS`). 404 si el local no existe o está inactivo. |
| GET | `/api/public/pedidos/{codigoSeguimiento}/estado-pago` | `consultaPublica` | Devuelve **solo** `Encontrado`, `Estado`, `MercadoPagoStatus`, `Total`, `LinkWhatsapp`. Sin datos personales. |
| POST | `/api/public/locales/{slug}/pedidos` | `pedidoPublico` | Crea pedido. Devuelve `PedidoCreateResponseDTO` con `ResumenWhatsApp` y `CodigoSeguimiento`. Definido en `PedidosController` con ruta absoluta. |
| POST | `/api/public/locales/{slug}/pedidos/{pedidoId}/preferencia-mp` | — | Crea la preferencia de pago. Body: `{ codigoSeguimiento }`. |
| POST | `/api/public/locales/{slug}/cupones/validar` | `consultaPublica` | Valida cupón contra un subtotal. `[AllowAnonymous]` en `CuponesController`. |
| GET | `/api/MercadoPago/oauth/callback` | — | Callback OAuth. `[AllowAnonymous]`; la seguridad la da el `state`. Redirige al front admin. |
| POST | `/api/MercadoPago/webhook` | — (a propósito) | Webhook de pagos. `[AllowAnonymous]`; la seguridad la da la firma HMAC. Responde 200 salvo error grave. |
| POST | `/api/MercadoPago/dev/simular-webhook-aprobado` | — | **Solo Development**: simula un pago aprobado (404 fuera de Development). |

> Los endpoints públicos de pedidos y cupones viven en controllers con ruta absoluta. `CuponesController` tiene `[Authorize]` a nivel de clase y `[AllowAnonymous]` en el endpoint público; en `PedidosController` el `[Authorize]` está por acción, no en la clase. Para resolver el local, los cuatro endpoints con `{slug}` usan `ObtenerActivoPorSlugAsync`; un local inactivo responde 404 en menú, pedidos y cupones, y 400 (`ValidacionException`) en preferencia-mp.

### Privados (JWT `Bearer`)

**Administrador** — `[Authorize]` a nivel de clase

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/Administrador/{id}` | Obtiene el negocio. Valida pertenencia y devuelve un DTO sin secretos. |
| PATCH | `/api/Administrador/{id}/local` | Patch parcial (incluye `SlugLocal` y `EsActivo`). Verifica `id == adminId` del token. Un slug inválido, reservado o en uso responde 400 `{ mensaje }`. |

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
| GET | `/api/Pedidos?estado=&desde=&hasta=&formaPago=&formaEntrega=&page=1&pageSize=25` | Lista paginada con filtros. Devuelve `PagedResultDTO<PedidoListItemResponseDTO>` (§6.12). |
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

**Configuración perezosa:** el constructor de `MercadoPagoService` **ya no lanza** si faltan claves de `MercadoPago:*`; las lee como nullables y las valida al usarlas (`RequireConfig`, que lanza `InvalidOperationException("<clave> no configurado")`). Motivo: el servicio se inyecta en `PedidosController`, y una config de MP ausente tumbaba también los pedidos en efectivo o transferencia. `CrearPreferenciaPago` resuelve el local con `ObtenerActivoPorSlugAsync`.

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

### 6.8 Slug del local (`SlugHelper` + `AdministradorRepository`)

**Fuente única de verdad.** `Helpers/SlugHelper.cs` define formato, reservados y generación; ningún otro código arma slugs. Los dos algoritmos incompatibles anteriores (SQL sin acentos vs. `Slugify()` en memoria) ya no existen.

- **Persistido:** `Administrador.SlugLocal`, `nvarchar(60)` NOT NULL, índice único. Migración `AddSlugLocalToAdministrador`: pobló los administradores existentes con valores fijos (para no cambiar las URLs vigentes) antes de pasar a NOT NULL.
- **Resolución:** `IAdministradorRepository.ObtenerActivoPorSlugAsync(slug)` normaliza (`Trim` + minúsculas), devuelve `null` si queda vacío, y consulta en SQL `EsActivo && SlugLocal == normalizado` (`AsNoTracking`). Es la única vía en menú, pedido público, preferencia MP y validación de cupón. **Un local inactivo no resuelve.** Un slug con mayúsculas resuelve; uno con acentos ("café") no, porque ningún slug persistido los tiene.
- **Formato válido:** `^[a-z0-9]+(-[a-z0-9]+)*$`, máximo 60 (`SlugHelper.MaxLength`).
- **`Slugify`:** minúsculas, sin acentos (ñ→n), sin puntuación; espacios, `_` y `-` colapsan en un guion; recorta a 60. Devuelve `""` si no queda nada alfanumérico (Register responde 400).
- **Reservados** (`SlugHelper.Reservados`, comparación ordinal): `admin, assets, api, www, static, public, login, health, status, blog, help, soporte, terminos, privacidad, well-known`. Existen porque colisionan con rutas del frontend. Los validan Register (vía `GenerarSlugUnicoAsync`) y el PATCH (`ValidarNuevoSlugAsync`).
- **Alta:** `POST /api/Auth/register` deriva el slug de `NombreLocal` con `Slugify` y `GenerarSlugUnicoAsync`, que ante reservado o duplicado agrega sufijo `-2`, `-3`, … (`ConSufijo`, recortando la base para no pasar de 60).
- **Cambio manual:** `PATCH /api/Administrador/{id}/local` con `SlugLocal` → `ValidarNuevoSlugAsync` (formato, reservado, unicidad excluyendo al propio admin) → 400 `{ mensaje }` si falla. **Renombrar `NombreLocal` no toca el slug.** Cambiar el slug a mano sí rompe los links ya compartidos.
- `ExisteSlugAsync` no mira reservados; solo `GenerarSlugUnicoAsync` y `ValidarNuevoSlugAsync` lo hacen. Dos altas simultáneas con el mismo nombre pueden chocar en el índice único y terminar en 500 (ventana mínima; el índice garantiza integridad).

### 6.9 `ForwardedHeaders` y por qué `ForwardLimit = 1`

Detrás del front end de Azure App Service, `RemoteIpAddress` sería la IP interna del proxy. Para recuperar la IP real (rate limiting, logs) y el esquema original (`UseHttpsRedirection` entra en loop de redirects sin `X-Forwarded-Proto`) se leen `X-Forwarded-For` y `X-Forwarded-Proto`.

Configuración (`Program.cs`): `ForwardedHeaders = XForwardedFor | XForwardedProto`, `ForwardLimit = 1`, `KnownNetworks` y `KnownProxies` vacíos (la IP del proxy de App Service es privada y cambia; por defecto solo se confía en loopback).

- **Por qué `ForwardLimit = 1`:** se toma **solo la entrada más a la derecha** del `X-Forwarded-For`, la que agregó el front end de Azure. Todo lo que el cliente inyecte en el header queda a la izquierda y se ignora. Con un límite mayor, un atacante podría falsificar su IP (y esquivar el rate limiting) mandando `X-Forwarded-For: 1.2.3.4`.
- **Doble registro:** App Service Linux ya define `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, que registra su propio middleware. Nuestro `Configure<ForwardedHeadersOptions>` se ejecuta después y **pisa sus opciones**. Por eso, si esa variable está en `true`, `Program.cs` **no** llama a `UseForwardedHeaders()`: una segunda pasada consumiría la siguiente entrada del XFF, justo la que controla el cliente.
- **Posición:** primero en el pipeline, antes del manejo de excepciones, `UseHttpsRedirection`, CORS, rate limiter y auth.
- **La trampa de Cloudflare (inferencia, no está en el código; verificar):** si se pone Cloudflare (u otro proxy/CDN) delante de la API, el XFF que llega a Azure pasa a ser `cliente, IP-de-Cloudflare`, y Azure agrega la IP del edge de Cloudflare como última entrada. Con `ForwardLimit = 1`, `RemoteIpAddress` sería **la IP de Cloudflare**, no la del comprador: todos los usuarios que entran por el mismo edge comparten partición de rate limit (se bloquean entre sí) y los logs pierden la IP real. Subir a `ForwardLimit = 2` arregla eso pero solo si **todo** el tráfico pasa por Cloudflare; si alguien pega directo al `*.azurewebsites.net`, la entrada de la izquierda la controla el cliente y vuelve el spoofing. Opciones seguras: restringir el origen a las IPs de Cloudflare, o leer `CF-Connecting-IP` solo cuando la conexión viene de rangos de Cloudflare. Lo mismo vale para Front Door o cualquier CDN. El comentario en `Program.cs` menciona Front Door/CDN, no Cloudflare.
- No hay endpoint de diagnóstico de IP: el temporal (`DiagnosticoController`) se agregó para validar esto y se eliminó.

### 6.10 Rate limiting

`Microsoft.AspNetCore.RateLimiting` (incluido en el framework). Ventana deslizante (5 segmentos), particionada por `RemoteIpAddress` (`"sin-ip"` si es nulo), `QueueLimit = 0`. Respuesta al exceder: **429** con `{ "error": "Demasiados intentos, probá de nuevo en unos minutos" }` (genérico, sin `Retry-After`); cada rechazo se loguea como warning con política, IP, método y ruta.

| Política | Límite | Ventana | Aplicada en |
|---|---|---|---|
| `login` | 5 | 5 min | `POST /api/Auth/login` |
| `register` | 3 | 15 min | `POST /api/Auth/register` |
| `pedidoPublico` | 10 | 10 min | `POST /api/public/locales/{slug}/pedidos` |
| `consultaPublica` | 30 | 1 min | `GET .../estado-pago` y `POST .../cupones/validar` |

Se aplica con `[EnableRateLimiting("nombre")]` por acción. **No hay límite global**: lo que no lleva el atributo no está limitado.

**Sin rate limit, a propósito o por omisión:**
- `POST /api/MercadoPago/webhook` — **a propósito**: MP reenvía y bursts legítimos no deben rebotar; la firma HMAC es la defensa.
- `GET /api/public/locales/{slug}/menu` — la caché de 60 s lo absorbe para slugs válidos. Un slug inexistente nunca se cachea (cada intento pega a la base).
- `POST .../pedidos/{pedidoId}/preferencia-mp` — pública; exige código de seguimiento válido pero no tiene límite.
- `GET /api/MercadoPago/oauth/callback`, y todos los endpoints autenticados (se apoyan en el JWT).

**Limitaciones:**
- **El estado vive en memoria de cada instancia.** Con N instancias del App Service el límite efectivo es N veces mayor y no se comparte; se reinicia con cada reinicio del proceso. Escalar a más de una instancia requiere store distribuido (Redis).
- Depende de §6.9: si la IP real no se recupera bien, todos comparten una partición o el límite se esquiva.
- Una IP compartida (NAT de oficina, CGNAT) comparte cupo: 5 logins por 5 min por IP.

### 6.11 Caché del menú público

Objetivo: el menú se arma con una consulta de cinco niveles de `Include` más imágenes y descuentos; es lo más pedido de la API.

- **`IMenuPublicoCache`** (singleton, sobre `IMemoryCache`). Clave: `menu-publico:{slugNormalizado}`. **TTL absoluto: 60 s.** Guarda el `MenuPublicoResponseDTO` ya mapeado más la ruta relativa del logo (`MenuPublicoCacheado`); la URL absoluta del logo depende del `Host` y se arma en cada respuesta.
- **Invalidación por local, no por clave:** hay un `CancellationTokenSource` por `administradorId`; cada entrada se registra con su `IChangeToken`. `Invalidar(adminId)` cancela el token y expulsa todas las entradas de ese local, bajo cualquier slug (cubre el cambio de slug).
- **Token antes de leer:** `PublicController.GetMenu` toma el token **antes** de consultar la base. Si el local se invalida mientras se arma el menú, la entrada nace expirada y no queda dato viejo.
- **Solo se cachea el éxito:** inexistente o inactivo → 404 sin cachear. Respuesta con header `X-Cache: HIT` o `MISS`.
- **`MenuCacheInvalidationInterceptor`** (`Data/`, scoped, registrado en el `DbContext`) es la **única** fuente de invalidación: antes de cada `SaveChanges` recorre el `ChangeTracker` y resuelve qué locales cambian el menú; al guardar invalida. Los controllers y services no conocen la caché. Cubre: `Categoria`, `Producto`, `Imagen`, `Descuento` (con `AdministradorId` propio); `Administrador` modificado o borrado (no recién agregado); y `ProductoExtra`, `TipoVariante`, `VarianteProducto` (por `ProductoId`) y `OpcionVariante` (por `TipoVarianteId` → `Producto`), que consultan el dueño en una query extra.
- **Transacciones explícitas:** los locales tocados dentro de una transacción se invalidan otra vez en el `Commit` (un GET entre `SavedChanges` y el commit podría recachear el dato viejo); un `Rollback` los descarta. Si `SaveChanges` falla, se limpian los pendientes.
- **El bypass de `ExecuteDeleteAsync`/`ExecuteUpdateAsync`:** estos métodos ejecutan SQL directo y **no pasan por `SaveChanges`**, así que el interceptor no los ve. Hoy hay un solo caso, `VarianteProductoRepository.EliminarTodas` (`ExecuteDeleteAsync`), que invalida a mano con `Invalidar(adminId)`. Lo mismo vale para SQL crudo y migraciones. Cualquier `ExecuteUpdate/Delete` nuevo sobre algo visible en el menú debe invalidar explícitamente.
- **Para agregar una entidad que se muestra en el menú:** sumarla al `switch` de `ResolverAdministradores`. Si no, el menú queda desactualizado hasta el TTL.
- **Límites:** (1) es caché **por instancia**: con varias instancias, una escritura invalida solo la instancia que la atendió y las demás sirven el menú viejo hasta 60 s. (2) El TTL también acota los descuentos con `FechaInicio`/`FechaFin`: que un descuento empiece o venza no dispara invalidación, se refleja en ≤ 60 s. (3) Los pedidos no invalidan, salvo que cambien stock de un producto/variante vía `SaveChanges` (sí invalida, porque el stock de la variante se expone en el menú).

### 6.12 Pedido público y listado paginado

**Creación (`PedidoService.CrearPublicoPorSlug`).** Validaciones en orden: al menos un detalle; `NombreCliente` y `TelefonoCliente` no vacíos (antes llegaban `null` y `SaveChanges` fallaba con NOT NULL → 500); `FormaEntrega` en la whitelist `Delivery` / `Retira` (comparación `OrdinalIgnoreCase`); local activo por slug; efectivo con monto; dirección si es delivery; cantidades > 0; producto disponible del tenant; variante válida; extras del producto; cupón. Los errores de entrada son `InvalidOperationException` (→ 400 con texto plano), `ValidacionException` para cupón (→ 400 `{ mensaje }`), `KeyNotFoundException` (→ 404). Cualquier otra excepción: 500 con mensaje genérico (el detalle solo va al log).

**Totales y `Subtotal`.** El `PedidoCreateResponseDTO` y el resumen de WhatsApp usan `Subtotal` = **neto** (productos + extras − descuentos − cupón), de modo que `Subtotal + CostoEnvio == Total`. El detalle admin y el ticket **no** siguen esa definición exacta (ver §8, punto 5).

**Listado (`GET /api/Pedidos`).** `page` ≥ 1 (default 1), `pageSize` 1–100 (default 25); fuera de rango lanza `ValidacionException` (hoy sin catch en el controller, §2). Filtros exactos por `estado`, `formaPago`, `formaEntrega` y rango `desde <= Fecha <= hasta`, siempre acotado por `AdministradorId`. **Orden determinista `Fecha DESC, Id DESC`** (el desempate por `Id` evita que filas con la misma fecha salten o se repitan entre páginas). La paginación y la **proyección a `PedidoListItemResponseDTO`** (`Id, Fecha, Estado, NombreCliente, FormaPago, FormaEntrega, Total, ItemsCount`) ocurren en SQL, sin `Include`; `ItemsCount` es un `COUNT` de `Detalles`. Respuesta `PagedResultDTO<T>`: `Items, Total, Page, PageSize, TotalPages`.

**`estado-pago`.** Busca por `CodigoSeguimiento` y devuelve únicamente `Encontrado, Estado, MercadoPagoStatus, Total, LinkWhatsapp`. Antes devolvía el resumen completo (nombre, teléfono, dirección). El código de seguimiento (12 hex en `XXXX-XXXX-XXXX`, derivado de un GUID) es el único secreto.

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
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | No (lo setea App Service Linux) | Si es `true`, `Program.cs` no aplica `UseForwardedHeaders` por su cuenta (§6.9). |
| `Storage:Provider` | No | `"AzureBlob"` o cualquier otro valor → local. |
| `Storage:Local:BasePath` | No | Default `uploads`. |
| `Storage:AzureBlob:ConnectionString` / `:ContainerName` | Si `Provider=AzureBlob` | Falla al construir el provider si faltan. |

> **`appsettings.Example.json` está incompleto.** Solo cubre `ConnectionStrings`, `JwtSettings`, `AdminRegistroKey`, `Logging` y `AllowedHosts`. Le faltan `Encryption`, `MercadoPago` y `Storage`. Alguien que siga solo ese archivo no logra levantar el proyecto con pagos ni con Blob Storage. **Pendiente de completar.**

### Perfiles locales

`launchSettings.json`: HTTP en `http://localhost:5202`, HTTPS en `https://localhost:7288;http://localhost:5202`, más un perfil IIS Express en `:62159`. `ASPNETCORE_ENVIRONMENT=Development`, `launchUrl` = `swagger`.

### Migraciones

24 migraciones, de `20250512004358_InitialCreate` a `20261005233626_AddIndexPedidoCodigoSeguimiento`. La evolución del esquema se lee como la del producto: pedidos y detalles → multi-tenancy (`20250915224651_multitenant-v1`) → dirección y envío → comentarios → imágenes → variantes y stock → descuentos y cupones → Mercado Pago en tres pasos → orden de categorías → `SlugLocal` único (`20261005164443`) → índice por `CodigoSeguimiento`.

> `AddSlugLocalToAdministrador` pobló los administradores existentes con slugs fijos por `Id` (para no cambiar URLs vigentes) antes de volver la columna NOT NULL; falla si queda alguno sin slug. Cualquier base nueva con filas previas necesita revisar esa migración.

**No hay `Database.Migrate()` en `Program.cs`**: las migraciones se aplican manualmente con `dotnet ef database update`.

### Seed

Bloque al final de `Program.cs`, **condicionado a `IsDevelopment()`**. Si la tabla `Administradores` está vacía crea un admin de prueba con credenciales fijas, `NombreLocal` vacío y `SlugLocal = "admin-prueba"` (necesario por el índice único NOT NULL). Para crear negocios reales, usar `POST /api/Auth/register`.

---

## 8. Deuda técnica y bugs conocidos

Ordenado por impacto.

1. **Filtros multi-tenant manuales.** `HasQueryFilter` comentado en `AppDbContext`. Toda consulta debe filtrar `AdministradorId` a mano; un olvido es fuga entre tenants. **Prioridad máxima.**

2. **`ValidacionException` no tiene traducción global.** El middleware de `Program.cs` la trata como 500; cada controller debe atraparla. Hoy `GET /api/Pedidos?page=0` (o `pageSize` fuera de 1–100), y `GET estado` / `POST desconectar` de MercadoPago ante un admin inexistente, devuelven 500 en lugar de 400. Además los formatos de error del borde no son uniformes: algunos devuelven `{ mensaje }`, otros texto plano (`BadRequest(ex.Message)` en el pedido público, `Unauthorized("...")` en Auth).

3. **`FormaEntrega`: whitelist case-insensitive, uso case-sensitive.** `CrearPublicoPorSlug` acepta `"delivery"` o `"DELIVERY"` (set `OrdinalIgnoreCase`), pero después compara `== "Delivery"` en la validación de dirección, el costo de envío, el resumen de WhatsApp y el ticket. Un `"delivery"` se guarda tal cual, no cobra envío, no exige dirección y se rotula "Retira en el local". Conviene normalizar al valor canónico tras validar.

4. **Slugs: resuelto, con dos cabos sueltos.** (a) El menú, el pedido, la preferencia de MP y los cupones ya resuelven por una única vía (§6.8). (b) `README.md` todavía describe los dos algoritmos y el slug derivado como deuda vigente (líneas ~74 y ~414): está desactualizado. (c) Un administrador que desactiva su local (`EsActivo = false`) deja de resolver en todos los endpoints públicos, pero los pedidos ya creados siguen consultables por `estado-pago`.

5. **`CostoEnvio` no se persiste como columna, y `Subtotal` no significa lo mismo en todos los DTOs.** Va sumado dentro de `Pedido.Total` y se reconstruye por resta en tres lugares, con fórmulas que no coinciden:
   - `PedidosController.Get(id)`: `Total - subtotalConDescuentos - extras`
   - `PedidoService.GetTicketAsync`: `Total - subtotal + MontoDescuentoCupon`
   - `GenerarResumenWhatsApp`: `Total - (subtotalBruto - descProductos - descCupón)`

   Estado real de `Subtotal`:
   - **Respuesta de creación** (`PedidoCreateResponseDTO`) y **resumen de WhatsApp**: neto con extras. `Subtotal + CostoEnvio == Total`.
   - **Detalle admin** (`PedidoDetailResponseDTO`): neto **sin extras** (`SubtotalSinDescuentos − descProductos − descCupón`; `SubtotalSinDescuentos` no incluye extras). Hay que sumar extras para llegar al `Total`.
   - **Ticket** (`TicketResponseDTO`): suma de líneas (precio con descuento de línea + extras) × cantidad; **no resta el cupón ni el descuento a pedido completo**. El `CostoEnvio` del ticket (`Total − subtotal + MontoDescuentoCupon`) sale mal cuando hubo un descuento a pedido completo.

   Frágil ante cualquier cambio en el cálculo de totales. La solución de fondo es persistir `CostoEnvio` y los extras en `Pedido`.

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

17. **Rate limiting y caché por instancia.** Ambos viven en memoria del proceso (§6.10, §6.11). Escalar a más de una instancia multiplica los límites y deja menús viejos hasta 60 s. Junto con el `state` de OAuth (punto 9) son tres razones para un store distribuido antes de escalar horizontalmente.

18. **`ForwardLimit = 1` supone App Service sin proxy delante.** Cualquier CDN o proxy nuevo (Cloudflare, Front Door) degrada la IP real a la IP del proxy. Ver §6.9.

19. **Código muerto en repos/servicios.** `ObtenerTodos()` se eliminó del repositorio y servicio de pedidos, pero sigue en `Administrador`, `DetallePedido`, `Producto` y `ProductoExtra` (interfaz, repo y servicio), sin ningún llamador. `IDetallePedidoService` no está registrado en DI y `IDetallePedidoRepository` está registrado dos veces. `PedidosController.Get(id)` arma el DTO leyendo `AppDbContext` directo, salteándose el servicio.

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

**Secretos:** ningún archivo con credenciales reales está trackeado actualmente. `appsettings.json` y `appsettings.Development.json` están cubiertos por `Eat_Experience/.gitignore`. `appsettings.Example.json` sí está versionado, pero solo con placeholders.

> **Historial:** hubo una fuga de credenciales por `build_temp/` (output de compilación commiteado por error) entre `b478f78` y `e990fd5`. Los archivos se eliminaron del árbol pero **siguen siendo recuperables desde el historial**, y el repositorio es público. Existe una auditoría completa fuera del repositorio con el detalle y el plan de rotación. **Rotar las credenciales es lo que revoca el acceso; borrar el archivo no.**

**Archivos trackeados que no deberían estarlo:** ninguno. `.vs/` y `obj/` se sacaron del índice en el commit `42c52ec`.

**`.gitignore`:** hay dos, uno en la raíz y otro en `Eat_Experience/`. Entre ambos la cobertura es correcta para `appsettings` reales, `.vs/`, `obj/`, `bin/`, `uploads/` y `build_temp/` (verificado con `git check-ignore`).
