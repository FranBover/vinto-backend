# Vinto — Backend (Vinto.Api) · Documentación técnica interna

> Documentación interna, más profunda que el README. Generada leyendo el código real del repositorio y verificada contra el estado actual de `main`.
> Donde algo no pudo confirmarse con certeza se marca **(verificar)**.
> **No** se incluyen valores de secretos: solo se referencian por nombre de configuración.

Última actualización: 2026-10-21 · Verificado contra el commit `720a097` (`main`, árbol limpio). Incluye la feature de horarios (§6.13), el proyecto de tests (§11) y el procedimiento de migraciones en Azure (§7).

---

## 1. Qué es Vinto

SaaS multi-tenant de tienda online para negocios chicos. Cada negocio (tenant) tiene su catálogo, su panel de administración y su tienda pública.

Nació para un local de comida que tomaba todos los pedidos por teléfono. El dominio se generalizó después: hoy modela productos, variantes, stock, cupones y pedidos sin supuestos sobre el rubro. Quedan restos del origen en la nomenclatura —`Administrador.NombreLocal`, la ruta pública `/locales/{slug}`, el endpoint `comanda`— que no se renombraron para no romper el frontend ni forzar migraciones.

Características que se desprenden del código:

- **Tienda pública por negocio**, accesible por *slug* persistido (`Administrador.SlugLocal`, ver §6.8) en `GET /api/public/locales/{slug}/menu`. Sin login del comprador. El menú se cachea 60 s (§6.11).
- **Pedidos sin registro**: el comprador arma el pedido contra el endpoint público. Se genera un código de seguimiento.
- **Resumen para WhatsApp**: al crear el pedido el backend devuelve `ResumenWhatsApp`, texto formateado en es-AR listo para enviar. Regenerable desde el panel.
- **Panel privado**: catálogo, variantes, stock, descuentos, cupones, pedidos, imágenes, datos del negocio, reportes y conexión con Mercado Pago. Protegido por JWT.
- **Pagos con Mercado Pago** vía OAuth (cada negocio conecta su propia cuenta), preferencias de Checkout Pro y webhook con firma validada.
- **Tiempo real** con SignalR (`/hubs/pedidos`): notifica nuevos pedidos y pagos confirmados al panel.
- **Horarios y apertura automática**: cada negocio carga sus horarios semanales (hasta 2 turnos por día, con turnos nocturnos) y su zona horaria, y puede cerrar a mano. El estado abierto/cerrado se calcula en cada request. Un local cerrado sigue mostrando su menú pero no acepta pedidos nuevos (409). Detalle en §6.13.

**Multi-tenant:**
- En lo **público**, el tenant se resuelve por **slug** (`Administrador.SlugLocal`, columna única, independiente de `NombreLocal`) vía `IAdministradorRepository.ObtenerActivoPorSlugAsync`. Un local con `EsActivo = false` (cuenta deshabilitada) no resuelve. Que un local esté **cerrado** (por horario o a mano) es otra cosa: sí resuelve (§6.13).
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

**Solución/proyecto:** `Eat_Experience.sln` en la raíz del repositorio; el proyecto `Vinto.Api` vive en el subdirectorio `Eat_Experience/`. La carpeta conserva el nombre viejo mientras que el `.csproj` y el namespace raíz ya son `Vinto.Api`. Por eso el pipeline referencia `**/Vinto.Api.csproj` y arranca con `dotnet Vinto.Api.dll`. La solución tiene un segundo proyecto, `Eat_Experience.Tests` (xUnit, §11). Fuera del código: `docs/sql/` guarda los scripts SQL de migración para Azure (§7) y `tools/` el script de verificación extremo a extremo (§11).

**Capas:**

```
Controllers/   → HTTP, extracción de adminId del JWT, validación de entrada, mapeo a DTO
Services/      → Lógica de negocio (Interfaces/ + Implementaciones/)
Repositories/  → Acceso a datos sobre AppDbContext (Interfaces/ + Implementaciones/)
DTOs/          → Contratos de request/response
Models/        → Entidades EF
Data/          → AppDbContext (relaciones, índices, precisión, defaults) + MenuCacheInvalidationInterceptor
Helpers/       → EncryptionHelper (AES-GCM), MercadoPagoSignatureValidator, SlugHelper, ValidacionException
                 Horarios (§6.13): HorarioLocalValidator, HorarioLocalMapper, HorarioLocalIntervalos, EstadoLocalPublico,
                 LocalCerradoMensaje, MenuPublicoArmador, LocalCerradoException, HorariosInvalidosException
Storage/       → IStorageProvider + LocalStorageProvider + AzureBlobStorageProvider
Hubs/          → PedidosHub (SignalR)
Migrations/    → 25 migraciones EF
```

> **Nota sobre la capa de repos:** su uso es parcial y deliberado. Varios controllers y services usan `AppDbContext` directamente cuando la consulta necesita múltiples `Include` (`PublicController.GetMenu` es el caso extremo, con cinco niveles de `ThenInclude`). Conviven ambos estilos; no hay convención única.

**Multi-tenant en la práctica:**

- Los filtros globales (`HasQueryFilter`) están **preparados pero comentados** en `AppDbContext` (líneas ~263-267), porque activarlos requiere inyectar un `ITenantProvider` en el constructor del contexto. El aislamiento se hace **manualmente** filtrando por `AdministradorId` en cada consulta. **Riesgo:** una consulta que olvide el filtro es una fuga entre tenants.
- Patrón en controllers privados: el claim `adminId` se lee en cada controller. Los de catálogo/promociones/imágenes/reportes tienen un `TryGetAdminId(out int)` **privado propio** (no hay uno compartido); `PedidosController`, `StockController` y `AdministradorController` lo leen inline (`ObtenerAdminId()` en Stock). Si falta el claim, Pedidos y Stock devuelven `Unauthorized()`; Administrador devuelve `Forbid()`. Luego cada operación valida pertenencia de la entidad.
- La unicidad es por tenant: índices compuestos `(AdministradorId, Nombre)` en `Categoria` y `Producto`, `(AdministradorId, Codigo)` en `Cupon`. El slug es único **global** (§6.8).

**Registro de servicios (`Program.cs`), en orden:**

JWT (dos esquemas: sesión por header y ticket del hub, §6.1) → `TimeProvider.System` (singleton: la fuente de "ahora" del cálculo de horarios) → `IMenuPublicoCache` (singleton) + `MenuCacheInvalidationInterceptor` (scoped) → DbContext SQL Server (command timeout 180s, con el interceptor) → 13 repositorios → 18 servicios de dominio (incluye `EstadoLocalCalculator`, `HorarioLocalService` y `EstadoLocalService`) → `Configure<ForwardedHeadersOptions>` → MemoryCache → HttpClient → `EncryptionHelper` y `MercadoPagoSignatureValidator` (singletons) → `IStorageProvider` condicional → `ImagenService` → SignalR → Controllers + Swagger → CORS `AllowFrontend` → `AddRateLimiter`.

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

> **Este middleware NO traduce `ValidacionException`.** Trata toda excepción como 500. **Verificado en producción el 2026-10-07:** `GET /api/Pedidos?page=0` devuelve 500, no 400. La traducción a 400/404 la hace cada controller con su propio `catch (ValidacionException)` (Cupones, Descuentos, MercadoPago parcialmente, Pedidos público, Administrador.PatchLocal, HorariosLocal). Donde el controller no la atrapa, el cliente recibe 500. Casos hoy sin catch: `GET /api/Pedidos` con `page`/`pageSize` inválidos, y `GET estado` y `POST desconectar` de MercadoPago cuando el administrador no existe. Además `ValidacionException.StatusCode` casi siempre se ignora: los catch devuelven 400. Las excepciones son `DescuentosController` (distingue el 404), `HorariosLocalController` (devuelve el `StatusCode` de la excepción: 409 ante una edición concurrente) y `PedidosController.CrearPedidoPublico`, que atrapa `LocalCerradoException` (subclase de `ValidacionException` con 409) **antes** que `ValidacionException`: con subclases que llevan otro código, el orden de los `catch` es parte del contrato.

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

**Login** (`POST /api/Auth/login`): busca por email, verifica con `PasswordHasher.VerifyHashedPassword`, rechaza si `EsActivo == false` (401 con mensaje propio; se evalúa después de la contraseña, así que no revela el estado de una cuenta a quien no la conoce) y emite el token. `EsActivo` significa solo "cuenta habilitada" (§6.13). **No se revisa al validar el JWT:** deshabilitar una cuenta no corta las sesiones ya emitidas, que siguen andando hasta que el token expire.

**Claims del JWT** (`AuthController.GenerateToken`):
- `sub` → email del administrador
- `adminId` → id del tenant (**es el que sostiene todo el aislamiento**)
- `jti` → GUID aleatorio

Firma HMAC-SHA256 con `JwtSettings:Key`. Expiración según `JwtSettings:ExpirationInMinutes`: la sesión del panel dura **720 min (12 h)**. En producción es el App Setting `JwtSettings__ExpirationInMinutes`; los `appsettings*.json` locales y `appsettings.Example.json` ya dicen 720. `ClockSkew` queda en el default (5 min), así que el servidor acepta el token hasta ~12 h 5 min. No hay refresh: al vencer, el panel vuelve al login (el front lee el claim `exp`, no tiene la duración fija).

**Validación** (`Program.cs`): valida issuer, audience, lifetime y firma. Todo activado. Hay dos esquemas `JwtBearer`, con los parámetros armados en `JwtTokens` (`ParametrosApi` / `ParametrosHub`):
- **Por defecto (`Bearer`)**: la sesión del panel, solo por el header `Authorization`. Para rutas `/hubs` no autentica nada (`NoResult`). Nunca lee el query string.
- **`HubTicket`**: el ticket del hub (§6.1), con otro `aud` (`<JwtSettings:Audience>.hub`) y tolerancia de reloj de 5 s en vez de los 5 min por defecto. Solo autentica rutas `/hubs`. Lo usa únicamente `PedidosHub` vía `[Authorize(AuthenticationSchemes = ...)]`.

Misma clave e issuer, audience distinto: un ticket no pasa como token de la API y una sesión no pasa como ticket (probado en `JwtTokensTests` y contra la API real). Por eso hacen falta dos esquemas: cada uno valida un solo `aud`, y con un único esquema habría que aceptar ambos audience a la vez.

**Registro** (`POST /api/Auth/register`): `[AllowAnonymous]`, protegido por el header `X-Admin-Key` comparado contra `AdminRegistroKey`. Verifica que el email sea único y genera el `SlugLocal` (§6.8). Login y registro tienen rate limit por IP (§6.10).

**Autorización por tenant:** no hay roles. El único eje es la pertenencia al tenant. Cada endpoint privado extrae `adminId` del token y valida que el recurso le pertenezca.

> No hay revocación de tokens: el `jti` se emite pero no se persiste ni se chequea contra una lista. Un token robado es válido hasta que expira, y con sesiones de 12 h esa ventana ya no es corta (ver también §8 punto 14). **(verificar si vale la pena una blacklist)**

---

## 4. Modelo de datos

Entidades reales de `Models/` y `AppDbContext`. `*` = nullable.

### Administrador (tenant)

Tabla central.
- `Id`, `Nombre`, `Email` (**índice único**), `PasswordHash`
- Negocio: `NombreLocal`, `SlugLocal` (`nvarchar(60)` NOT NULL, **índice único** `IX_Administradores_SlugLocal`; no cambia al renombrar `NombreLocal`), `Direccion`, `Telefono`, `LinkWhatsapp*`, `LogoUrl*`, `Horarios*` (**texto libre legado**, ver §6.13), `UbicacionUrl*`
- Horarios (§6.13): `ZonaHoraria` (`nvarchar(64)` NOT NULL, id IANA, default `America/Argentina/Buenos_Aires`), `CerradoManualmente` (bit, default false)
- `EsActivo` (**solo "cuenta habilitada"**, no "local abierto"; §6.13), `FechaRegistro` (default `GETUTCDATE()`), `UltimoAcceso*`, `PlanSuscripcion*`, `DominioPersonalizado*`
- Transferencia: `AliasTransferencia*`, `TitularCuenta*`
- Envío: `ZonaEnvio` (`"Ciudad"` | `"Nacional"`, default `"Nacional"`), `CostoEnvio*`
- Stock: `StockBajoAlerta*` (default 5), `AutoDeshabilitarSinStock` (default false)
- Mercado Pago: `MercadoPagoUserId*`, `MercadoPagoAccessToken*` (**cifrado**), `MercadoPagoRefreshToken*` (**cifrado**), `MercadoPagoPublicKey*`, `MercadoPagoTokenExpiresAt*`, `MercadoPagoConectado`

Relaciones 1:N, todas `Restrict` salvo `Imagen` y `HorarioLocal` (Cascade): Categoria, Producto, Pedido, Descuento, Cupon, MovimientoStock, Imagen, ComentarioPedido, HorarioLocal.

### Catálogo

**Categoria** — `Id`, `Nombre`, `Orden` (default 0), `AdministradorId`. Índice único `(AdministradorId, Nombre)`.

**Producto** — `Id`, `Nombre`, `Descripcion*`, `Precio` (18,2), `ImagenUrl`, `Disponible` (default true), `CategoriaId`, `AdministradorId`, `TieneVariantes` (default false), `Stock*` (stock simple cuando no tiene variantes). Colecciones: `Extras`, `TiposVariante`, `Variantes`. Índice único `(AdministradorId, Nombre)`.

**ProductoExtra** — `Id`, `Nombre`, `PrecioAdicional` (18,2), `ProductoId` (Restrict).

### Variantes (dos niveles)

**TipoVariante** — `Id`, `ProductoId` (Cascade), `Nombre`, `Orden`. Máximo 2 por producto, validado en el controller.

**OpcionVariante** — `Id`, `TipoVarianteId` (Cascade), `Valor`, `Orden`.

**VarianteProducto** — combinación concreta: `Id`, `ProductoId` (Cascade), `Opcion1Id` (Restrict), `Opcion2Id*` (Restrict), `Precio` (18,2), `Stock*`, `Disponible`, `Sku*`.

> El diseño separa la **definición** de las dimensiones de las **combinaciones** concretas. Permite que cada combinación tenga precio y stock propios sin duplicar la definición.

### Horarios del local

**HorarioLocal** (tabla `HorariosLocal`) — una fila por día de la semana y por local: `Id`, `AdministradorId` (Cascade), `DiaSemana` (0-6, orden de `System.DayOfWeek`: **0 = domingo**), `Modo` (`nvarchar(10)`: `"Cerrado"` | `"24h"` | `"Turnos"`; constantes en `ModoHorario`, comparación ordinal), `Turno1Apertura*`, `Turno1Cierre*`, `Turno2Apertura*`, `Turno2Cierre*` (`time`). Índice único `(AdministradorId, DiaSemana)`; `CHECK` de base `DiaSemana BETWEEN 0 AND 6` y `Modo IN ('Cerrado','24h','Turnos')`. El `CHECK` de `Modo` es una red de seguridad: la collation es case-insensitive y aceptaría `"turnos"`, el código solo `"Turnos"`. Las reglas, el cálculo del estado y los endpoints están en §6.13.

### Ventas

**Pedido**
- `Id`, `AdministradorId` (Restrict), `Fecha` (default `GETUTCDATE()`), `Estado` (default `"Pendiente"`), `CodigoSeguimiento*`
- Cliente: `NombreCliente`, `TelefonoCliente`
- Pago/entrega: `FormaPago`, `FormaEntrega`, `MontoPagoEfectivo*`, `DireccionCliente*`, `ReferenciaDireccion*`, `UbicacionUrl*`
- Totales: `Total` (18,2), `SubtotalSinDescuentos`, `MontoDescuentoProductos`, `MontoDescuentoCupon` (todos default 0)
- Cupón: `CuponId*` (Restrict), `CodigoCupon*`
- Mercado Pago: `MercadoPagoPreferenceId*`, `MercadoPagoPaymentId*`, `MercadoPagoStatus*`, `MercadoPagoStatusDetail*`, `MercadoPagoFechaPago*`, `MercadoPagoCollectionId*`
- Colección `Detalles` (Cascade). Índices `(AdministradorId, Fecha)` (sostiene el listado paginado) y `CodigoSeguimiento` (para `estado-pago`; no es único)
- `FormaEntrega` solo se crea como `Delivery` o `Retira` desde el endpoint público (whitelist, §6.12)

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

- **Todo campo monetario es `decimal(18,2)`**, declarado con `HasPrecision` (o `[Precision]` en `PagoMercadoPago.Monto`). Nunca punto flotante. **Única excepción hoy: `Administrador.CostoEnvio`**, sin precisión explícita: la columna igual queda `decimal(18,2)` por el default del proveedor, pero EF emite un warning (30000) en cada `dotnet ef`. Conviene declararla.
- **Fechas en UTC**, con default `GETUTCDATE()` en: `Administrador.FechaRegistro`, `Pedido.Fecha`, `Imagen.FechaCreacion`, `MovimientoStock.FechaCreacion`, `Descuento/Cupon/UsoCupon/DetallePedidoDescuento.FechaCreacion`. La conversión a horario argentino ocurre solo en `ReporteService`.
- **`DeleteBehavior.Restrict` por defecto.** Cascade solo donde el hijo no tiene sentido sin el padre (detalles de pedido, opciones de variante, imágenes del tenant). Un producto vendido alguna vez no se puede borrar.

---

## 5. Endpoints

Rutas reales tomadas de los controllers. `[controller]` resuelve al nombre sin sufijo.

### Públicos (sin JWT)

La columna *Rate limit* es la política de §6.10; "—" = sin límite.

| Método | Ruta | Rate limit | Qué hace |
|---|---|---|---|
| POST | `/api/Auth/register` | `register` | Registra un negocio. Requiere header `X-Admin-Key`. Genera el `SlugLocal` desde `NombreLocal`. |
| POST | `/api/Auth/login` | `login` | Login, devuelve `{ token }`. |
| GET | `/api/public/locales/{slug}/menu` | — | Catálogo público (cacheado 60 s, header `X-Cache: HIT/MISS`). 404 si el local no existe o su cuenta está deshabilitada (`EsActivo = false`); un local **cerrado** responde 200. Suma `estado` (se calcula en cada request, fuera del caché) y `horario` (§6.13). |
| GET | `/api/public/pedidos/{codigoSeguimiento}/estado-pago` | `consultaPublica` | Devuelve **solo** `Encontrado`, `Estado`, `MercadoPagoStatus`, `Total`, `LinkWhatsapp`. Sin datos personales. |
| POST | `/api/public/locales/{slug}/pedidos` | `pedidoPublico` | Crea pedido. Devuelve `PedidoCreateResponseDTO` con `ResumenWhatsApp` y `CodigoSeguimiento`. Definido en `PedidosController` con ruta absoluta. **409 `{ mensaje, codigo: "LocalCerrado", proximoCambio }`** si el local está cerrado (§6.13). |
| POST | `/api/public/locales/{slug}/pedidos/{pedidoId}/preferencia-mp` | — | Crea la preferencia de pago. Body: `{ codigoSeguimiento }`. |
| POST | `/api/public/locales/{slug}/cupones/validar` | `consultaPublica` | Valida cupón contra un subtotal. `[AllowAnonymous]` en `CuponesController`. |
| GET | `/api/MercadoPago/oauth/callback` | — | Callback OAuth. `[AllowAnonymous]`; la seguridad la da el `state`. Redirige al front admin. |
| POST | `/api/MercadoPago/webhook` | — (a propósito) | Webhook de pagos. `[AllowAnonymous]`; la seguridad la da la firma HMAC. Responde 200 salvo error grave. |
| POST | `/api/MercadoPago/dev/simular-webhook-aprobado` | — | **Solo Development**: simula un pago aprobado (404 fuera de Development). |

> Los endpoints públicos de pedidos y cupones viven en controllers con ruta absoluta. `CuponesController` tiene `[Authorize]` a nivel de clase y `[AllowAnonymous]` en el endpoint público; en `PedidosController` el `[Authorize]` está por acción, no en la clase. Para resolver el local, los cuatro endpoints con `{slug}` usan `ObtenerActivoPorSlugAsync`; un local con la cuenta deshabilitada (`EsActivo = false`) responde 404 en menú, pedidos y cupones, y 400 (`ValidacionException`) en preferencia-mp. Un local cerrado resuelve normalmente en los cuatro; solo el alta de pedido lo rechaza (§6.13).

### Privados (JWT `Bearer`)

**Administrador** — `[Authorize]` a nivel de clase

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/Administrador/{id}` | Obtiene el negocio. Valida pertenencia y devuelve un DTO sin secretos. |
| PATCH | `/api/Administrador/{id}/local` | Patch parcial (incluye `SlugLocal`; **ya no acepta `EsActivo`**: un `esActivo` en el body se ignora, §6.13). Verifica `id == adminId` del token. Un slug inválido, reservado o en uso responde 400 `{ mensaje }`. No cambia `ZonaHoraria` ni `CerradoManualmente` (viven en los endpoints de abajo), pero el `UPDATE` los incluye con el valor leído al empezar el request (§8, punto 21). |

**Hub** — `HubController`, `[Authorize]`, ruta base `api/Hub`

| Método | Ruta | Qué hace |
|---|---|---|
| POST | `/api/Hub/ticket` | Emite el ticket de 60 s para abrir la conexión de SignalR (§6.1). Sin body. 200 `{ ticket, expiraEnSegundos }`; 401 si falta/venció el JWT de sesión o si la cuenta no está habilitada (`EsActivo = false`, `{ mensaje }`); 403 si el JWT no trae `adminId`. Sin rate limit. |

**Horarios y cierre manual del local** — `HorariosLocalController`, `[Authorize]`, ruta base `api/Administrador/local`. Ninguna ruta ni DTO lleva un id de administrador: el local sale del claim `adminId` (si falta, `Forbid()`; si el admin no existe, 404).

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/Administrador/local/horarios` | `{ zonaHoraria, cerradoManualmente, dias[], estado }`. `dias` vacío = sin horarios fijos (siempre abierto). |
| PUT | `/api/Administrador/local/horarios` | Reemplaza zona y horarios en una sola transacción. Body `{ zonaHoraria, dias[] }`. Responde lo mismo que el GET. 400 `{ mensaje, errores[] }` si no valida; 409 `{ mensaje }` ante una edición concurrente. |
| PATCH | `/api/Administrador/local/cierre-manual` | Body `{ cerrado: true \| false }`. Devuelve el `EstadoLocal` resultante. 400 si falta `cerrado`. |

> Reglas de los horarios, formato de `estado` y comportamiento ante errores: §6.13. Estos tres endpoints no tienen rate limit (como el resto de los autenticados).

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

`PedidosHub` tiene `[Authorize(AuthenticationSchemes = "HubTicket")]` a nivel de clase. En `OnConnectedAsync` **deriva el grupo del claim `adminId` del ticket** y suscribe la conexión ahí. No expone métodos `JoinGroup`/`LeaveGroup` invocables por el cliente: si los expusiera, cualquier ticket válido podría suscribirse al grupo de otro tenant y recibir sus pedidos en vivo. SignalR remueve la conexión de sus grupos al desconectarse, así que no hace falta limpieza manual.

**El hub no acepta el JWT de sesión: acepta un ticket de 60 s.** El navegador no puede mandar el header `Authorization` en el WebSocket, así que el token tiene que ir en la URL (`?access_token=`). Con sesiones de 12 h, un JWT en la URL terminaría en cualquier log que registre la request (Application Insights, logs HTTP de App Service). Por eso el flujo es:

1. **Login** (`POST /api/Auth/login`) → JWT de sesión (12 h). Vive en `localStorage`, solo viaja en el header `Authorization`.
2. **Ticket** (`POST /api/Hub/ticket`, `[Authorize]`, sin body): `HubTicketService` verifica en la base que el admin exista y tenga `EsActivo = true` (si no, **401** `{ mensaje }`) y emite un JWT firmado con la misma clave, `aud = <JwtSettings:Audience>.hub`, claims `adminId` y `jti`, vida de 60 s (`JwtTokens.DuracionTicketSegundos`). Responde `{ ticket, expiraEnSegundos }`. Es stateless: no se persiste ni es de un solo uso, y se pide uno nuevo por cada vez que SignalR llama a `accessTokenFactory`.
3. **Conexión**: el cliente llama a `accessTokenFactory` (que ahora pide un ticket) en el `negotiate`, donde SignalR manda el ticket en el header `Authorization`, y de nuevo al abrir el WebSocket, donde va en el query string. Son dos tickets por intento. Un ticket vencido o con otro `aud` (por ejemplo, el JWT de sesión) da **401** en ambos.
4. **Conexión abierta**: `MapHub` fija `CloseOnAuthenticationExpiration = false` de forma explícita. El ticket solo sirve para *abrir*; si SignalR cerrara la conexión al vencer, el panel se reconectaría cada minuto. Costo asumido: una conexión abierta sigue viva aunque la sesión venza o la cuenta se deshabilite. El panel la cierra cuando expira la sesión, y no hay forma de abrir una nueva sin pasar por el paso 2.
5. **Reconexión**: cada intento (`withAutomaticReconnect` y el reintento manual de 5 s) vuelve a pasar por `accessTokenFactory`, o sea por el paso 2 con el JWT de sesión que haya en `localStorage` en ese momento. Si la cuenta se deshabilitó, el ticket da 401 y la conexión no se recupera.

Sin `[EnableRateLimiting]` a propósito (§6.10): requiere sesión, cada local tiene varios dispositivos detrás de una misma IP pública y, con la conexión caída, cada dispositivo pide hasta dos tickets cada 5 s. Cualquier límite por IP lo compartirían todos los dispositivos de un local y trabaría justo las reconexiones.

**Un ticket no sirve en la API y una sesión no sirve en el hub.** Ver §3: dos esquemas `JwtBearer`, uno por `aud`. El esquema por defecto nunca lee el query string.

**Logs.** Ningún middleware propio loguea el query string (el handler de excepciones y el rate limiter loguean solo `Path`), no hay `UseHttpLogging` y los mensajes de `JwtBearer`/`IdentityModel` ocultan el token (PII oculta). El log `Request starting/finished` de `Microsoft.AspNetCore.Hosting.Diagnostics` (Information, con el `QueryString` completo) sí lo imprimiría, y como `appsettings.json` no se despliega, en producción corre con el nivel por defecto: `Program.cs` lo filtra a `Warning` con `builder.Logging.AddFilter(...)`. **No lo saques**: con el ticket ya no es el JWT de sesión lo que queda expuesto, pero un ticket sigue siendo usable durante 60 s.

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

Es el único subsistema con manejo explícito de zona horaria: resuelve `TimeZoneInfo` de Argentina una vez en un campo estático, convierte UTC → Argentina para calcular los rangos, y de vuelta a UTC para consultar. Sin esto, "ventas de hoy" arrancaría a las 21:00 del día anterior. Usa Argentina **fija**: no lee `Administrador.ZonaHoraria`, que sí gobierna el cálculo de horarios (§6.13). Un local con otra zona vería sus reportes en hora argentina.

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
- **Estado actual: no hay Cloudflare delante de la API.** Todos los registros DNS de `vintoapp.com` están en modo "DNS only" (nube gris), así que la app recibe tráfico directo del front end de Azure y `ForwardLimit = 1` es correcto.
- **La trampa de Cloudflare (condicional):** SI algún día se pasa `api.vintoapp.com` a proxy (nube naranja) u otro proxy/CDN, el XFF que llega a Azure pasa a ser `cliente, IP-de-Cloudflare`, y Azure agrega la IP del edge de Cloudflare como última entrada. Con `ForwardLimit = 1`, `RemoteIpAddress` sería **la IP de Cloudflare**, no la del comprador: todos los usuarios que entran por el mismo edge comparten partición de rate limit (se bloquean entre sí) y los logs pierden la IP real. Subir a `ForwardLimit = 2` arregla eso pero solo si **todo** el tráfico pasa por Cloudflare; si alguien pega directo al `*.azurewebsites.net`, la entrada de la izquierda la controla el cliente y vuelve el spoofing. Opciones seguras: restringir el origen a las IPs de Cloudflare, o leer `CF-Connecting-IP` solo cuando la conexión viene de rangos de Cloudflare. Lo mismo vale para Front Door o cualquier CDN. Antes de activar la nube naranja hay que resolver esto. El comentario en `Program.cs` menciona Front Door/CDN, no Cloudflare.
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

- **`IMenuPublicoCache`** (singleton, sobre `IMemoryCache`). Clave: `menu-publico:{slugNormalizado}`. **TTL absoluto: 60 s.** Guarda el `MenuPublicoResponseDTO` ya mapeado, la ruta del logo y los **datos crudos de horario** (`MenuPublicoCacheado` + `HorarioMenuCacheado`: id del local, zona, cierre manual y filas de `HorarioLocal`). La URL absoluta del logo depende del `Host` y se arma en cada respuesta (solo se antepone el host a rutas relativas; con Blob Storage el valor guardado ya es absoluto y sale tal cual).
- **El estado del local nunca se cachea.** El caché guarda *datos* (zona, cierre manual, filas), no el *resultado*, que depende de la hora. `MenuPublicoArmador.Armar` recibe el `EstadoLocal` ya calculado y arma la respuesta sobre la entrada cacheada sin mutarla; en HIT y en MISS el estado se evalúa con `TimeProvider`. La entrada cacheada lleva `Estado = null`. Por eso el local figura cerrado a las 23:00 aunque el menú siga en HIT: el paso del tiempo no dispara invalidación (§6.13).
- **Invalidación por local, no por clave:** hay un `CancellationTokenSource` por `administradorId`; cada entrada se registra con su `IChangeToken`. `Invalidar(adminId)` cancela el token y expulsa todas las entradas de ese local, bajo cualquier slug (cubre el cambio de slug).
- **Token antes de leer:** `PublicController.GetMenu` toma el token **antes** de consultar la base. Si el local se invalida mientras se arma el menú, la entrada nace expirada y no queda dato viejo. Como el slug se resuelve *antes* de tomar el token, el local se **relee por id después** de tomarlo: así lo que se cachea del local (zona, cierre manual, nombre) es posterior al token.
- **Solo se cachea el éxito:** inexistente o con la cuenta deshabilitada → 404 sin cachear. Respuesta con header `X-Cache: HIT` o `MISS`.
- **`MenuCacheInvalidationInterceptor`** (`Data/`, scoped, registrado en el `DbContext`) es la **única** fuente de invalidación: antes de cada `SaveChanges` recorre el `ChangeTracker` y resuelve qué locales cambian el menú; al guardar invalida. Los controllers y services no conocen la caché. Cubre: `Categoria`, `Producto`, `Imagen`, `Descuento`, `HorarioLocal` (con `AdministradorId` propio); `Administrador` modificado o borrado (no recién agregado; incluye `ZonaHoraria` y `CerradoManualmente`); y `ProductoExtra`, `TipoVariante`, `VarianteProducto` (por `ProductoId`) y `OpcionVariante` (por `TipoVarianteId` → `Producto`), que consultan el dueño en una query extra.
- **Transacciones explícitas:** los locales tocados dentro de una transacción se invalidan otra vez en el `Commit` (un GET entre `SavedChanges` y el commit podría recachear el dato viejo); un `Rollback` los descarta. Si `SaveChanges` falla, se limpian los pendientes.
- **El bypass de `ExecuteDeleteAsync`/`ExecuteUpdateAsync`:** estos métodos ejecutan SQL directo y **no pasan por `SaveChanges`**, así que el interceptor no los ve. Hoy hay un solo caso, `VarianteProductoRepository.EliminarTodas` (`ExecuteDeleteAsync`), que invalida a mano con `Invalidar(adminId)`. Lo mismo vale para SQL crudo y migraciones: un `UPDATE Administradores SET EsActivo = 0` a mano deja el menú cacheado hasta 60 s (por instancia). Cualquier `ExecuteUpdate/Delete` nuevo sobre algo visible en el menú debe invalidar explícitamente. Por eso `HorarioLocalService` guarda por el change tracker y no con `ExecuteDelete`.
- **Para agregar una entidad que se muestra en el menú:** sumarla al `switch` de `ResolverAdministradores`. Si no, el menú queda desactualizado hasta el TTL.
- **Límites:** (1) es caché **por instancia**: con varias instancias, una escritura invalida solo la instancia que la atendió y las demás sirven el menú viejo hasta 60 s. (2) El TTL también acota los descuentos con `FechaInicio`/`FechaFin`: que un descuento empiece o venza no dispara invalidación, se refleja en ≤ 60 s (los evalúa con `DateTime.UtcNow` *dentro* de lo que se cachea, a diferencia del estado del local; ver §8, punto 20). (3) Los pedidos no invalidan, salvo que cambien stock de un producto/variante vía `SaveChanges` (sí invalida, porque el stock de la variante se expone en el menú).

### 6.12 Pedido público y listado paginado

**Creación (`PedidoService.CrearPublicoPorSlug`).** Validaciones en orden: al menos un detalle; `NombreCliente` y `TelefonoCliente` no vacíos (antes llegaban `null` y `SaveChanges` fallaba con NOT NULL → 500); `FormaEntrega` en la whitelist `Delivery` / `Retira` (comparación `OrdinalIgnoreCase`); local activo por slug (404 si la cuenta está deshabilitada); **local abierto** (409 `LocalCerrado`, §6.13: va antes de leer productos, así un rechazo no crea pedido ni mueve stock); efectivo con monto; dirección si es delivery; cantidades > 0; producto disponible del tenant; variante válida; extras del producto; cupón. Los errores de entrada son `InvalidOperationException` (→ 400 con texto plano), `ValidacionException` para cupón (→ 400 `{ mensaje }`), `LocalCerradoException` (→ 409 `{ mensaje, codigo, proximoCambio }`, atrapada antes que `ValidacionException`), `KeyNotFoundException` (→ 404). Cualquier otra excepción: 500 con mensaje genérico (el detalle solo va al log).

**Totales y `Subtotal`.** El `PedidoCreateResponseDTO` y el resumen de WhatsApp usan `Subtotal` = **neto** (productos + extras − descuentos − cupón), de modo que `Subtotal + CostoEnvio == Total`. El detalle admin y el ticket **no** siguen esa definición exacta (ver §8, punto 5).

**Listado (`GET /api/Pedidos`).** `page` ≥ 1 (default 1), `pageSize` 1–100 (default 25); fuera de rango lanza `ValidacionException` (sin catch en el controller: 500 verificado en producción el 2026-10-07, §2). Filtros exactos por `estado`, `formaPago`, `formaEntrega` y rango `desde <= Fecha <= hasta`, siempre acotado por `AdministradorId`. **Orden determinista `Fecha DESC, Id DESC`** (el desempate por `Id` evita que filas con la misma fecha salten o se repitan entre páginas). La paginación y la **proyección a `PedidoListItemResponseDTO`** (`Id, Fecha, Estado, NombreCliente, FormaPago, FormaEntrega, Total, ItemsCount`) ocurren en SQL, sin `Include`; `ItemsCount` es un `COUNT` de `Detalles`. Respuesta `PagedResultDTO<T>`: `Items, Total, Page, PageSize, TotalPages`.

**`estado-pago`.** Busca por `CodigoSeguimiento` y devuelve únicamente `Encontrado, Estado, MercadoPagoStatus, Total, LinkWhatsapp`. Antes devolvía el resumen completo (nombre, teléfono, dirección). El código de seguimiento (12 hex en `XXXX-XXXX-XXXX`, derivado de un GUID) es el único secreto.

### 6.13 Horarios y estado del local

**Dos conceptos separados.** Antes de esta feature `EsActivo` hacía de "cuenta habilitada" y de "local abierto" a la vez, y el dueño lo alternaba con `PATCH /api/Administrador/{id}/local`. El problema: el login rechaza `EsActivo = false`, así que el dueño que "cerraba" su local quedaba afuera de su propio panel y no podía volver a abrirlo. Hoy son dos cosas distintas:

| | Qué es | Quién lo cambia | Efecto |
|---|---|---|---|
| `Administrador.EsActivo` | **Solo** "cuenta habilitada" | Nadie desde la API (se toca en la base) | `false` → el login responde 401 y el slug deja de resolver: menú, pedido y cupón dan 404; preferencia-mp, 400. |
| Abierto / cerrado | `CerradoManualmente` + `HorarioLocal` + `ZonaHoraria` | El dueño, desde `api/Administrador/local/...` | Un local cerrado **sigue existiendo**: el menú responde 200 con `estado.estaAbierto = false`. Lo único que se rechaza es iniciar un pedido (409). |

- `AdministradorLocalUpdateDTO` ya no tiene `EsActivo`: un `esActivo` en el body del PATCH se ignora (lo chequea el script de verificación, §11). `AdministradorResponseDTO.EsActivo` sigue en las respuestas, solo lectura.
- Deshabilitar una cuenta **no corta los JWT ya emitidos** (§3) y, si se hace por SQL, el menú cacheado tarda hasta 60 s en dar 404 (§6.11).
- **La migración no toca `EsActivo`.** Un local que hubiera quedado en `false` por el toggle viejo sigue sin poder loguearse ni resolver hasta que alguien lo rehabilite a mano en la base. Conviene revisar `SELECT Id, NombreLocal FROM Administradores WHERE EsActivo = 0` antes de dar por cerrada la migración de datos.

**Cálculo del estado** (`EstadoLocalCalculator.Calcular`, función pura: no accede a base ni al reloj; el instante entra por parámetro desde `TimeProvider`). Precedencia:

1. `CerradoManualmente` → cerrado, motivo `CerradoManualmente`, `ProximoCambio = null`. Gana sobre todo y se evalúa antes que la zona (no la necesita).
2. Sin ninguna fila de `HorarioLocal` → abierto, motivo `SinHorarios`: **sin horarios cargados, el local está siempre abierto.**
3. Con filas → motivo `Horario`: abierto o cerrado según el día y la hora **en la zona del local**.

El resultado es `EstadoLocal(EstaAbierto, Motivo, ProximoCambio)`. `ProximoCambio` (UTC) es el próximo cierre si está abierto y la próxima apertura si está cerrado; es `null` si no hay un cambio dentro de los próximos 7 días (semana entera cerrada, o abierto 24 h los 7 días) o si el cierre es manual. El cálculo arma los tramos de ayer a una semana adelante y los fusiona: un cierre que coincide con la apertura siguiente no cuenta como cambio.

**Reglas del horario.** Las impone `HorarioLocalValidator` (con mensajes en español, todos juntos) y la definición de qué intervalos cubre un día vive en un solo lugar, `HorarioLocalIntervalos`, que comparten el validador y la calculadora para no discrepar:

- **0 o 7 días.** Cero filas = sin horarios fijos (siempre abierto). Si hay filas tienen que ser exactamente 7, una por `DiaSemana` (0 = domingo … 6 = sábado, sin repetidos).
- **Modo** (ordinal, mayúsculas exactas): `Cerrado`, `24h` o `Turnos`. `Cerrado` y `24h` no pueden traer turnos.
- **Máximo 2 turnos por día.** `Turnos` exige el turno 1; el 2 es opcional y no se puede usar sin el 1. Cada turno lleva apertura y cierre; `apertura == cierre` se rechaza (para las 24 horas existe el modo `24h`).
- **Turno nocturno: cierre < apertura = termina al día siguiente** (20:00 → 01:00). Cuenta aunque el día siguiente figure `Cerrado`. Con dos turnos, **solo el turno 2 puede cruzar la medianoche**, y tiene que empezar en o después del cierre del turno 1.
- **Apertura inclusiva, cierre exclusivo:** el local está abierto en `[apertura, cierre)`. A las 19:00 en punto abre; a las 23:00 en punto ya cerró.
- **Sin superposiciones**, tratando la semana como circular: un nocturno del sábado que pasa la medianoche no puede pisar un turno del domingo. Que un cierre coincida con la apertura siguiente no es superponerse.
- **Horas** como `"HH:mm"` (se aceptan también `"H:mm"` y `"HH:mm:ss"`; vacío = sin valor); siempre se devuelven `"HH:mm"`. `"00:00"` es medianoche.
- **Zona horaria por local**: `Administrador.ZonaHoraria`, id IANA de hasta 64 caracteres, obligatoria en el PUT. Se valida con `TimeZoneInfo.TryFindSystemTimeZoneById`, así que "válida" significa "la conoce el host" (tzdata/ICU del App Service). El día y la hora se evalúan en esa zona, nunca en UTC; todos los instantes de la API salen en UTC. En un cambio de horario de verano, una hora inexistente o repetida se resuelve con el offset estándar (convención del código; Argentina no tiene horario de verano).

**Panel** (rutas en §5). Comportamiento que no se ve en la tabla:

- `PUT` reemplaza **todo** en un único `SaveChanges`: borra las filas viejas, inserta las nuevas y actualiza la zona, o no cambia nada. Va por el change tracker (no `ExecuteDelete`) para que el interceptor invalide el menú. `dias: []` vacía los horarios (el local queda siempre abierto); `dias` ausente o `null` es **400** (no se interpreta como "vaciar"); la zona ausente también.
- Respuestas del `PUT`: 400 `{ mensaje: "Los horarios no son válidos.", errores: [...] }` (`HorariosInvalidosException`, incluye horas mal escritas y `"dias": [null]`); 409 `{ mensaje }` si otra edición se cruzó (`DbUpdateConcurrencyException` o violación del índice único `(AdministradorId, DiaSemana)`).
- `PATCH cierre-manual`: `Cerrado` es `bool?` a propósito. Un body sin el campo es 400; si se leyera como `false` reabriría el local por accidente. No toca `EsActivo`.
- **El panel es estricto, no falla abierto.** Con horarios cargados y una zona inválida guardada, `GET horarios` responde 500 (el calculador lanza `ArgumentException`) y `PATCH cierre-manual {cerrado:false}` persiste el cambio y *después* responde 500. El `PUT` sirve para corregir la zona. Es deliberado: el dueño tiene que enterarse.

**Menú público** (`GET .../menu`). Suma dos campos al contrato:

- `estado` (`{ estaAbierto, motivo, proximoCambio }`): se calcula **en cada request**, HIT o MISS (`PublicController.CalcularEstado`, con `TimeProvider`), sobre los datos de horario que viajan en la entrada cacheada. Ver §6.11.
- `horario` (`{ zonaHoraria, dias[] }`, mismo formato `"HH:mm"` que el GET del panel): sí se cachea, y se invalida al cambiar horarios, zona o cierre manual por `MenuCacheInvalidationInterceptor`. `dias` vacío = sin horarios fijos.
- Un local cerrado **no da 404**: devuelve categorías y productos como siempre.

**Pedido con el local cerrado: 409 `LocalCerrado`.** `PedidoService.CrearPublicoPorSlug` llama a `IEstadoLocalService.AsegurarAbiertoAsync` justo después de resolver el slug y antes de leer productos o escribir nada. Orden de rechazos: forma del body (400) → slug (404) → local cerrado (409) → resto de validaciones. Lanza `LocalCerradoException` (subclase de `ValidacionException`, `StatusCode = 409`, con `Codigo = "LocalCerrado"` y `ProximoCambio`), y `PedidosController.CrearPedidoPublico` la atrapa **antes** que `ValidacionException` y responde `409 { mensaje, codigo: "LocalCerrado", proximoCambio }`. El mensaje lo arma `LocalCerradoMensaje` en la zona del local: `"El local está cerrado. Abre hoy a las 19:00."` (o `mañana`, `el jueves`, `el próximo jueves`; `a la 01:00` para la una de la madrugada). Con cierre manual, o sin una próxima apertura, dice `"El local está cerrado por el momento."` sin prometer hora. El estado que decide y el que redacta el mensaje salen del mismo instante.

**Qué NO se bloquea con el local cerrado, y por qué:**

- `POST .../pedidos/{pedidoId}/preferencia-mp` (pagar un pedido ya creado): el pedido se aceptó cuando el local estaba abierto; bloquear el pago dejaría pedidos tomados que no se pueden cobrar.
- `POST .../cupones/validar`: es una cotización de solo lectura, no inicia nada.
- `GET .../estado-pago`, el webhook de Mercado Pago y todo el panel (cambiar estado de pedidos, stock, etc.): operan sobre pedidos que ya existen.

Regla práctica: lo único que el cierre impide es **iniciar** un pedido. Todo flujo público nuevo que cree datos del comprador debería decidir explícitamente si pasa por `AsegurarAbiertoAsync`.

**Fallar abierto (solo lo público).** `EstadoLocalPublico.Calcular` envuelve el cálculo en un `try/catch` amplio: si falla (típicamente una zona horaria inválida en la base), loguea un **`LogError`** con el id del local y la zona, y devuelve abierto con motivo `SinHorarios`. Lo usan el menú y el pedido: la tienda no se cae ni deja de vender por un dato de horario roto. Consecuencias: un local con zona rota figura **abierto aunque tenga todo cerrado**, y la única señal es el log (buscar `se lo trata como abierto`). `LocalCerradoMensaje` también degrada: con zona rota informa sin hora en vez de fallar. El cierre manual no necesita zona, así que no pasa por este camino.

**Campos legado en el menú.** Se conservan porque el front desplegado los lee; el front nuevo debe usar `estado` y `horario`.

- `local.esActivo`: antes era la cuenta habilitada. Hoy el menú solo se sirve a cuentas habilitadas, así que valdría siempre `true`; por compatibilidad `MenuPublicoArmador` lo **pisa con `estado.estaAbierto`** en cada respuesta (copia vía `LocalInfoDTO.ParaRespuesta`, sin mutar la entrada cacheada). En el JSON público significa "abierto ahora", no "cuenta habilitada".
- `local.horarios`: el texto libre (`Administrador.Horarios`). Se sigue devolviendo tal cual y se sigue pudiendo escribir con `PATCH .../local` (`horarios`). No tiene relación con los horarios estructurados.
- **Cuándo se pueden sacar:** cuando ningún front (tienda y panel) los lea. Eso no se puede verificar desde este repo: hay que mirar los repos de frontend. Al sacarlos: quitar el parámetro `esActivo` de `ParaRespuesta` y `LocalInfoDTO.EsActivo`; quitar `LocalInfoDTO.Horarios`; actualizar `MenuPublicoArmadorTests`, que fija la compatibilidad a propósito, y los chequeos [12], [15], [16] y [19] del script (§11), que leen `local.esActivo` y `local.horarios`; y recién después, en una migración aparte, borrar la columna `Administradores.Horarios` junto con `AdministradorResponseDTO.Horarios` y el campo del PATCH.

**Datos existentes tras migrar.** Los locales que ya existían quedaron con `ZonaHoraria = America/Argentina/Buenos_Aires`, `CerradoManualmente = false` y cero filas de `HorarioLocal`: siempre abiertos hasta que el dueño cargue horarios.

---

## 7. Configuración y entorno

### Claves requeridas

| Clave | Requerida | Notas |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Sí | SQL Server. Command timeout 180s configurado en código. |
| `JwtSettings:Key` | Sí | HMAC-SHA256. Falla al arrancar si falta. |
| `JwtSettings:Issuer` / `:Audience` | Sí | Validados en cada request. |
| `JwtSettings:ExpirationInMinutes` | Sí | Sesión del panel: 720 (12 h). En Azure: `JwtSettings__ExpirationInMinutes`. Si falta vale 0 (el token nace vencido y solo anda ~5 min por el `ClockSkew`). |
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

25 migraciones, de `20250512004358_InitialCreate` a `20261020074552_AddHorarioLocalYCierreManual`. La evolución del esquema se lee como la del producto: pedidos y detalles → multi-tenancy (`20250915224651_multitenant-v1`) → dirección y envío → comentarios → imágenes → variantes y stock → descuentos y cupones → Mercado Pago en tres pasos → orden de categorías → `SlugLocal` único (`20261005164443`) → índice por `CodigoSeguimiento` → horarios (`20261020074552`: columnas `Administradores.ZonaHoraria` y `CerradoManualmente` + tabla `HorariosLocal`; es **aditiva**, con defaults, y no toca datos existentes).

> `AddSlugLocalToAdministrador` pobló los administradores existentes con slugs fijos por `Id` (para no cambiar URLs vigentes) antes de volver la columna NOT NULL; falla si queda alguno sin slug. Cualquier base nueva con filas previas necesita revisar esa migración.

**No hay `Database.Migrate()` en `Program.cs`**: las migraciones se aplican a mano. En desarrollo, con `dotnet ef database update`. En producción (Azure SQL) **no** se usa `ef database update`: se genera un script SQL y se corre en el Query editor del portal. Procedimiento abajo.

#### Aplicar una migración en producción (Azure)

El orden importa: **migrar antes de que el código nuevo reciba tráfico.** Una migración aditiva como la de horarios deja funcionando al código viejo sobre el esquema nuevo, pero no al revés: EF selecciona todas las columnas mapeadas de `Administradores`, así que el código nuevo sobre el esquema viejo falla (500) en login, menú y casi todo.

1. **Backup previo** de la base de producción, antes de correr nada. No hay script de rollback versionado: el camino de vuelta es restaurar. *(Cómo se hace el backup en este entorno —punto de restauración, copia o export— no está documentado acá: completar.)*
2. **Ver cuál fue la última migración aplicada en producción:** `SELECT TOP 1 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;`
3. **Generar el script idempotente desde esa migración** (corrido desde la raíz del repo):
   ```
   dotnet ef migrations script <ULTIMA_APLICADA> --idempotent --project Eat_Experience/Vinto.Api.csproj --output docs/sql/<NUEVA_MIGRACION>.sql
   ```
   El "desde" es el **argumento posicional `<FROM>`**; el CLI de `dotnet ef` no tiene un flag `--from` (en la consola del administrador de paquetes de Visual Studio sí es `Script-Migration -From`). Con `--idempotent` cada paso se protege con `IF NOT EXISTS (… __EFMigrationsHistory …)`, así que correrlo dos veces no rompe nada. El comando también imprime el warning del `CostoEnvio` sin precisión (§4); es inofensivo.
4. **Editar el script antes de pegarlo.** La salida cruda de EF no sirve tal cual para el Query editor del portal:
   - Agregar `SET XACT_ABORT ON;` como **primera línea**. Sin eso, un error de sentencia no aborta la transacción: SQL Server sigue con las siguientes y el `COMMIT` final confirma un estado a medias (columnas sin tabla, o al revés, y la migración marcada como aplicada).
   - **Borrar la línea `GO`** (EF la agrega después del `COMMIT`). `GO` es un separador de lotes del cliente `sqlcmd`/SSMS, no una instrucción de T-SQL, y el Query editor del portal no la entiende.
   - El archivo versionado en `docs/sql/` es hoy la **salida cruda de EF**: tiene el `GO` final y no tiene `SET XACT_ABORT ON`. Hay que hacerle estos dos ajustes al correrlo (o versionar la versión ya ajustada).
5. **Correr el script** en el Query editor del portal, conectado a la base de producción (no a otra).
6. **Verificar después.** Que la migración quedó registrada y que el esquema es el esperado. Para horarios:
   ```sql
   SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId = N'20261020074552_AddHorarioLocalYCierreManual';
   SELECT COUNT(*) FROM HorariosLocal;                                   -- 0 filas
   SELECT TOP 5 Id, ZonaHoraria, CerradoManualmente FROM Administradores; -- defaults aplicados
   SELECT name FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('HorariosLocal');
   ```
   Y recién después, un GET real a la API (menú de un local) para confirmar que responde 200.

Si la migración falla a mitad de camino y `XACT_ABORT` estaba activo, la transacción entera se revierte y no queda nada aplicado: se corrige y se vuelve a correr.

### Seed

Bloque al final de `Program.cs`, **condicionado a `IsDevelopment()`**. Si la tabla `Administradores` está vacía crea un admin de prueba con credenciales fijas, `NombreLocal` vacío y `SlugLocal = "admin-prueba"` (necesario por el índice único NOT NULL). Para crear negocios reales, usar `POST /api/Auth/register`.

---

## 8. Deuda técnica y bugs conocidos

Ordenado por impacto.

1. **Filtros multi-tenant manuales.** `HasQueryFilter` comentado en `AppDbContext`. Toda consulta debe filtrar `AdministradorId` a mano; un olvido es fuga entre tenants. **Prioridad máxima.**

2. **`ValidacionException` no tiene traducción global.** El middleware de `Program.cs` la trata como 500; cada controller debe atraparla. Hoy `GET /api/Pedidos?page=0` (verificado en producción el 2026-10-07) (o `pageSize` fuera de 1–100), y `GET estado` / `POST desconectar` de MercadoPago ante un admin inexistente, devuelven 500 en lugar de 400. Además los formatos de error del borde no son uniformes: algunos devuelven `{ mensaje }`, otros texto plano (`BadRequest(ex.Message)` en el pedido público, `Unauthorized("...")` en Auth).

3. **`FormaEntrega`: whitelist case-insensitive, uso case-sensitive.** `CrearPublicoPorSlug` acepta `"delivery"` o `"DELIVERY"` (set `OrdinalIgnoreCase`), pero después compara `== "Delivery"` en la validación de dirección, el costo de envío, el resumen de WhatsApp y el ticket. Un `"delivery"` se guarda tal cual, no cobra envío, no exige dirección y se rotula "Retira en el local". Conviene normalizar al valor canónico tras validar.

4. **Slugs: resuelto, con dos cabos sueltos.** (a) El menú, el pedido, la preferencia de MP y los cupones ya resuelven por una única vía (§6.8). (b) `README.md` todavía describe los dos algoritmos y el slug derivado como deuda vigente (líneas ~74 y ~414), dice que hay 22 migraciones (son 25) y no menciona los horarios ni el proyecto de tests: está desactualizado. (c) Una cuenta deshabilitada (`EsActivo = false`, que hoy solo se cambia en la base) deja de resolver en todos los endpoints públicos, pero los pedidos ya creados siguen consultables por `estado-pago`. Un local solo *cerrado* (§6.13) sí resuelve.

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

14. **Sin revocación de tokens.** El `jti` se emite pero no se persiste ni se valida. `EsActivo` solo se revisa en el login, al resolver el slug público y al emitir el ticket del hub; no al validar el JWT de sesión. Una cuenta deshabilitada con una sesión vigente conserva **todo** el panel (catálogo, pedidos, Mercado Pago, reportes) hasta 12 h, aunque su tienda pública ya no resuelva. Lo único que pierde enseguida es poder abrir conexiones nuevas del hub; las ya abiertas siguen vivas (§6.1). Cambiar la contraseña tampoco invalida las sesiones ya emitidas.

15. **Cobertura de tests mínima y fuera del pipeline.** Existe `Eat_Experience.Tests` (§11), pero cubre solo la lógica pura de horarios y estado del local (148 tests). Pedidos, descuentos, cupones, stock, Mercado Pago, aislamiento multi-tenant, caché e interceptor, y todos los controllers y servicios con `DbContext` no tienen tests. El pipeline no los corre (punto 23). Sigue siendo la deuda más grande.

16. **`appsettings.Example.json` incompleto.** Ver §7.

17. **Rate limiting y caché por instancia.** Ambos viven en memoria del proceso (§6.10, §6.11). Escalar a más de una instancia multiplica los límites y deja menús viejos hasta 60 s. Junto con el `state` de OAuth (punto 9) son tres razones para un store distribuido antes de escalar horizontalmente.

18. **`ForwardLimit = 1` supone App Service sin proxy delante.** Hoy es así (DNS only en Cloudflare). Si se agrega un CDN o proxy (nube naranja, Front Door) se degrada la IP real a la IP del proxy. Ver §6.9.

19. **Código muerto en repos/servicios.** `ObtenerTodos()` se eliminó del repositorio y servicio de pedidos, pero sigue en `Administrador`, `DetallePedido`, `Producto` y `ProductoExtra` (interfaz, repo y servicio), sin ningún llamador. `IDetallePedidoService` no está registrado en DI y `IDetallePedidoRepository` está registrado dos veces. `PedidosController.Get(id)` arma el DTO leyendo `AppDbContext` directo, salteándose el servicio. Ver también el punto 22 (`IAdministradorService`).

20. **Descuentos evaluados con `DateTime.UtcNow` dentro de lo que se cachea.** `PublicController.GetMenu` filtra los descuentos vigentes con `DateTime.UtcNow` mientras arma la entrada del menú (y calcula ahí los precios con descuento), así que un descuento que empieza o vence se refleja con hasta 60 s de atraso, por instancia. El pedido (`PedidoService.CrearPublicoPorSlug`) los evalúa con el reloj real: durante esa ventana el menú puede mostrar un precio que el pedido ya no aplica, o al revés. El estado del local resolvió el mismo problema dejando la hora fuera del caché; con los descuentos no es trivial porque el precio con descuento se cachea ya calculado. Además `Program.cs` declara a `TimeProvider` como "fuente única de ahora", pero hoy solo lo usan los horarios: el resto sigue con `DateTime.UtcNow` (descuentos, cupones, pedidos, reportes, Mercado Pago), y por eso no se puede probar con un reloj falso.

21. **`PatchLocal` reescribe todas las columnas de `Administrador`.** `AdministradorRepository.Actualizar` hace `Update(entidad)`, que marca *todas* las propiedades como modificadas: el `UPDATE` incluye `EsActivo`, `ZonaHoraria`, `CerradoManualmente`, los tokens de Mercado Pago, etc., con el valor leído al empezar el request. Si entre la lectura y el guardado cambia otra columna (el dueño cierra el local desde otra pestaña, o alguien cambia `EsActivo` en la base), el PATCH la pisa con el valor viejo. No hay `rowversion` ni token de concurrencia. El script (§11) solo verifica el caso secuencial. Además invalida el menú aunque no cambie nada visible. Arreglo: asignar solo lo que cambia (sin `Update()`) o agregar un token de concurrencia.

22. **Métodos latentes en `IAdministradorService`.** `ObtenerTodos`, `Crear` y `Eliminar` (y sus pares en el repositorio) no tienen ningún llamador: el controller usa solo `ObtenerPorId`, `Actualizar` y `ValidarNuevoSlugAsync`. Es un riesgo latente: `ObtenerTodos` devuelve **todos** los tenants y `Eliminar(id)` borra cualquiera sin validar pertenencia, así que cablearlos a un endpoint sin filtrar por `adminId` abriría una fuga o un borrado entre tenants (el script, sección [13], prueba esas rutas con el token de otro local y verifica que el segundo tenant queda intacto). Conviene borrarlos. Lo mismo, a menor escala, con `IEstadoLocalService.ObtenerAsync`: sin llamadores (hoy solo se usa `AsegurarAbiertoAsync`), aunque sería la base del punto 24.

23. **El pipeline no corre `dotnet test`.** `azure-pipelines.yml` hace publish y deploy; un test roto no frena el despliegue. Falta un paso `DotNetCoreCLI@2` con `command: test` sobre `Eat_Experience.Tests` antes del publish (los 148 tests tardan menos de un segundo).

24. **No hay un endpoint liviano de estado.** Para saber si el local abrió o cerró, el front tiene que volver a pedir el menú completo (barato en HIT, pero devuelve todo el catálogo). Un `GET /api/public/locales/{slug}/estado` que resuelva el slug por `ObtenerActivoPorSlugAsync`, reutilice los datos de horario del caché y devuelva solo el `EstadoLocal` evitaría eso. Al ser público necesita `[EnableRateLimiting]` explícito (§6.10).

25. **Reportes ignora la zona horaria del local.** `ReporteService` usa Argentina fija (§6.7) mientras que los horarios usan `Administrador.ZonaHoraria`. Con un local fuera de Argentina, "ventas de hoy" y "abierto ahora" hablarían de días distintos.

26. **Caracteres de reemplazo (`�`) en mensajes al cliente.** `PedidosController` (líneas ~192-229) y `PublicController` (~227-228) tienen `U+FFFD` guardado literalmente en los fuentes (los archivos son UTF-8 válido, así que no es un problema de lectura): el comprador recibe `"Ocurri� un error al crear el pedido. Intent� nuevamente."` en los 500 de pedido público, preferencia de pago y estado-pago. Hay que reescribir esas cadenas y comentarios con sus tildes.

27. **El ticket del hub todavía viaja en una URL (acotado) y falta cerrar el despliegue.** Resuelto en el backend: el JWT de sesión ya no se acepta por query string en ningún lado y lo que queda en la URL del WebSocket (Application Insights, logs HTTP de App Service) es un ticket de 60 s (§6.1). Cuidado con el orden de despliegue: el front viejo manda el JWT de sesión al hub y recibe 401, así que **backend y front tienen que salir juntos** (el front nuevo pide el ticket a `POST /api/Hub/ticket`). Recién con el front nuevo en producción conviene subir `JwtSettings__ExpirationInMinutes=720` en Azure. Quedan en Application Insights los JWT de sesión de 60 min guardados antes del cambio (retención de 90 días; ese dato no figura en el repo): ya están vencidos. Un ticket captado sigue sirviendo 60 s (+5 s de tolerancia) para abrir *una* conexión de ese local.

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

**El pipeline no corre los tests** (no hay paso `dotnet test`; §8, punto 23) y **no aplica migraciones**: el esquema se migra a mano antes del deploy (§7).

> **Trampa: durante el despliegue la API puede devolver 500 un rato.** Mientras corre el pipeline (publish, `AzureWebApp@1`, reinicio del App Service Linux) las requests pueden fallar con 500 o no responder. Se observó al desplegar la feature de horarios. **Antes de diagnosticar un 500 después de un push, esperar a que el pipeline termine en verde y repetir la request.** Si con el pipeline en verde sigue el 500, el siguiente sospechoso es una migración sin aplicar (§7): el código nuevo sobre el esquema viejo falla en login y menú. Y recién después, el log de la API (el cuerpo del 500 en producción es genérico).

**Configuración en producción:** Application Settings del App Service. En **Linux** el separador de secciones es doble guion bajo, no dos puntos: `JwtSettings__Key`, `Encryption__Key`, `MercadoPago__ClientSecret`, `Storage__AzureBlob__ConnectionString`.

**Sin GitHub Actions.** No existe `.github/workflows`.

---

## 10. Higiene del repositorio

**Secretos:** ningún archivo con credenciales reales está trackeado actualmente. `appsettings.json` y `appsettings.Development.json` están cubiertos por `Eat_Experience/.gitignore`. `appsettings.Example.json` sí está versionado, pero solo con placeholders.

> **Historial:** hubo una fuga de credenciales por `build_temp/` (output de compilación commiteado por error) entre `b478f78` y `e990fd5`. Los archivos se eliminaron del árbol pero **siguen siendo recuperables desde el historial**, y el repositorio es público. Existe una auditoría completa fuera del repositorio con el detalle y el plan de rotación. **Rotar las credenciales es lo que revoca el acceso; borrar el archivo no.**

**Archivos trackeados que no deberían estarlo:** ninguno. `.vs/` y `obj/` se sacaron del índice en el commit `42c52ec`.

**`.gitignore`:** hay dos, uno en la raíz y otro en `Eat_Experience/`. Entre ambos la cobertura es correcta para `appsettings` reales, `.vs/`, `obj/`, `bin/`, `uploads/` y `build_temp/` (verificado con `git check-ignore`). `Eat_Experience.Tests/bin` y `obj` quedan cubiertos por las reglas genéricas `[Bb]in/` y `[Oo]bj/` de la raíz.

---

## 11. Tests y verificación

### Proyecto de tests (`Eat_Experience.Tests`)

xUnit 2.9.2 sobre net9.0 (`Microsoft.NET.Test.Sdk` 17.12.0, `xunit.runner.visualstudio` 2.8.2), con referencia a `Vinto.Api.csproj`. Forma parte de `Eat_Experience.sln`, así que `dotnet build Eat_Experience.sln` también lo compila.

**Cómo correrlo:** `dotnet test Eat_Experience.sln` (o `dotnet test Eat_Experience.Tests`). Estado al 2026-10-21: **148 tests, todos pasan** (~0,3 s). No necesitan base de datos, red ni `appsettings`.

**Qué cubre.** Solo lógica pura de horarios y estado del local (nada toca `DbContext` ni HTTP):

| Archivo | Cubre |
|---|---|
| `EstadoLocalCalculatorTests` | `EstadoLocalCalculator`: sin horarios, cierre manual, turnos (13:59 abierto, 14:00 en punto cerrado, apertura inclusiva), turno nocturno que desborda sobre un día que figura cerrado, 24 h y todo cerrado (sin cambio previsible), zonas horarias (el día se decide en la zona, no en UTC), `ProximoCambio` exacto, días sin fila = cerrados, datos corruptos que nunca abren el local por error, y el salto del horario de verano. |
| `HorarioLocalValidatorTests` | Todas las reglas de §6.13: 0 o 7 días, día fuera de 0-6 y repetidos, modo y turnos cargados, `apertura == cierre`, orden de los turnos, superposiciones entre días (semana circular), zona inválida, acumulación de varios errores y nombres de día. |
| `HorarioLocalMapperTests` | Parseo y formato de horas (`TryParsearHora`, `Formatear`) y conversión DTO ↔ entidad. |
| `EstadoLocalPublicoTests` | "Fallar abierto": zona inválida o cualquier falla del cálculo → abierto + `LogError` con la causa; y lo que **no** es fallar abierto (un cierre real sigue cerrado, el cierre manual no necesita zona). |
| `LocalCerradoMensajeTests` | El texto del 409: hoy / mañana / día de la semana / próximo, hora en la zona del local, "a la 01:00", cierre manual sin hora, zona rota sin hora, y que `LocalCerradoException` sea 409 con código y próximo cambio. |
| `MenuPublicoArmadorTests` | Compatibilidad `local.esActivo = estado.estaAbierto`, que la entrada cacheada no se mute, que la misma entrada dé respuestas distintas según el estado, y armado del logo (relativo con host / absoluto sin host). |

Helpers: `Horarios` arma semanas de prueba y convierte hora de pared argentina (UTC-3) a UTC; `FakeLogger` captura lo que se loguea. Los nombres de test son oraciones en español con guiones bajos.

**Qué NO cubre** (y por eso existe el script de abajo): `HorarioLocalService` (la transacción del reemplazo, el índice único, el 409 por edición concurrente), `EstadoLocalService`, `PedidoService.CrearPublicoPorSlug`, los controllers (forma de los 400/409, orden de los `catch`), `MenuCacheInvalidationInterceptor` y `MenuPublicoCache`, las migraciones y el rate limiting. Todo lo demás del sistema (pedidos, descuentos, cupones, stock, MP) tampoco tiene tests (§8, punto 15). El pipeline no los ejecuta (§8, punto 23).

### Script de verificación extremo a extremo (`tools/verificar-horarios-local.ps1`)

Verifica contra la **API real y una base local** lo que los tests unitarios no pueden: persistencia, transacción de reemplazo, índice único, invalidación del caché (`X-Cache`), JWT, aislamiento entre tenants y rate limiting. Son 21 secciones ([0] a [20]): auth de los endpoints, GET/PUT de horarios (válidos, reemplazo, varias formas de rechazo, el tenant sale del JWT), estado con el reloj real, cierre manual, que `esActivo` ya no se escriba, auditoría de rutas sobre otro `Administrador`, aislamiento entre dos locales, menú público (`estado`, `horario`, compatibilidad de `esActivo`), el estado cambiando con el menú en HIT, el 409 del pedido (cierre manual, horario, todo cerrado), los flujos que no se bloquean (cupón, preferencia de pago, `estado-pago`), fallar abierto con zona inválida y que `EsActivo` siga definiendo el 404.

- **Solo local:** se niega a correr si `BaseUrl` no es localhost o si el SQL Server no es local.
- **Crea y borra sus datos:** admins descartables con email `verif-api-*@example.test` (más categoría, producto y pedidos), borrados al empezar y al terminar; compara el estado de los admins existentes antes y después. No toca los que ya existían.
- **Requisitos:** `sqlcmd` en el PATH, la base local (`EatExperienceDb` en `localhost\SQLEXPRESS`, autenticación Windows) migrada a la última migración, y la clave de registro (`-AdminKey`, la variable `VINTO_ADMIN_KEY` o `AdminRegistroKey` de los `appsettings` locales; nunca se imprime).
- **Uso:** `dotnet build Eat_Experience.sln` y después `powershell -ExecutionPolicy Bypass -File tools\verificar-horarios-local.ps1 -IniciarApi`. `-IniciarApi` levanta la API desde `bin\Debug` (puerto 5299 por defecto, `-BaseUrl` para cambiarlo) y la apaga al terminar; se recomienda porque el rate limiting es en memoria y cada corrida usa 2 registros, 4 logins y 7 pedidos públicos (límites: 3 / 5 / 10), así que sin API nueva una segunda corrida seguida puede dar 429. Además permite verificar la línea de error del log al fallar abierto (`-ApiLog` hace lo mismo con una API ya corriendo).
- **`-SinEspera`** omite el caso [16], que espera ~1 minuto para ver el estado pasar de abierto a cerrado con el menú en HIT.
- Sale con código 1 si alguna verificación falla. Compatible con Windows PowerShell 5.1 (el archivo es solo ASCII: los acentos van escapados).
- Hay que correrlo al tocar horarios, estado del local, el menú público o el alta de pedido público.
