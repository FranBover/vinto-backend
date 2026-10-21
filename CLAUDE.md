# Vinto — guía para Claude Code

Vinto es un SaaS multi-tenant de tienda online para negocios chicos: cada negocio tiene su catálogo, su panel privado y su tienda pública sin login del comprador.
Este repo es el backend: **ASP.NET Core .NET 9 + EF Core 9 (SQL Server) + JWT + SignalR**, con Mercado Pago, Azure Blob Storage e ImageSharp.

La solución `Eat_Experience.sln` está en la raíz; el proyecto es `Eat_Experience/Vinto.Api.csproj` (la carpeta conserva el nombre viejo, el proyecto y el namespace ya son `Vinto.Api`). Tests en `Eat_Experience.Tests/`; el script de verificación local en `tools/`.

## Convenciones no negociables

- **DTOs siempre en el borde HTTP.** Los controllers reciben y devuelven tipos de `DTOs/` (`*CreateDTO`, `*UpdateDTO`, `*ResponseDTO`, `*RequestDTO`). Nunca exponer ni aceptar entidades EF de `Models/` en un endpoint.
- **`adminId` sale del JWT, nunca del request.** Se lee del claim `adminId` (la mayoría de los controllers tiene su propio `TryGetAdminId` privado; `PedidosController`, `StockController` y `AdministradorController` lo leen inline). Si falta, cortar la request. Ningún DTO de entrada lleva `AdministradorId`.
- **Aislamiento manual por tenant.** Los `HasQueryFilter` están comentados en `AppDbContext`. Toda consulta filtra `AdministradorId` a mano, y toda operación sobre una entidad existente valida pertenencia antes de tocarla. Un olvido es fuga entre tenants.
- **Capas:** `Controllers/` (HTTP, adminId, mapeo a DTO) → `Services/` (`Interfaces/` + `Implementaciones/`) → `Repositories/` / `AppDbContext`. La capa de repos es parcial y convive con acceso directo al contexto: seguí el estilo del archivo que estés tocando.
- **Nomenclatura en español** para entidades, servicios y propiedades (`Producto`, `PedidoService`, `NombreLocal`). Mantenerla.
- **Errores de negocio:** `ValidacionException(mensaje, statusCode)` en services. **El middleware global de `Program.cs` NO la traduce** (todo lo no capturado sale como 500 genérico): el controller tiene que atraparla (verificado en producción: `GET /api/Pedidos?page=0` da 500) (`catch (ValidacionException ex)` → `BadRequest(new { mensaje = ex.Message })`). Si agregás un service que la lanza, revisá que su controller la capture. `LocalCerradoException` (409) es subclase: capturala **antes** que `ValidacionException`.
- **Plata en `decimal` con `HasPrecision(18, 2)`** en `AppDbContext`. Toda columna monetaria nueva va igual.
- **Un local se resuelve por slug SOLO con `IAdministradorRepository.ObtenerActivoPorSlugAsync`.** Ya filtra cuenta habilitada (`EsActivo`) + `SlugLocal` normalizado en SQL. No filtres por slug a mano.
- **`EsActivo` significa SOLO "cuenta habilitada"** (login y resolución del slug). Abierto/cerrado es otra cosa: `CerradoManualmente` + `HorarioLocal` + `ZonaHoraria`, resuelto por `IEstadoLocalCalculator`. No uses `EsActivo` para abrir/cerrar un local ni lo aceptes en un DTO de escritura: el toggle viejo dejaba al dueño sin poder loguearse para reabrir.

## No hagas esto

- **No renombres** la carpeta `Eat_Experience/`, ni `Administrador.NombreLocal`, ni la ruta `/locales/{slug}`, ni el endpoint `comanda`. Son restos del origen del proyecto y romperlos rompe el frontend o fuerza migraciones.
- **No toques `Response.Clear()`** ni muevas el middleware de excepciones por debajo de `UseCors`: está arriba a propósito para no borrar los headers CORS y que un 500 no se reporte como error de CORS.
- **No generes ni valides slugs fuera de `SlugHelper`.** Es la única definición (formato, reservados, `Slugify`). `SlugLocal` es columna persistida con índice único y **no cambia al renombrar `NombreLocal`**. Un slug nuevo reservado/duplicado/mal formado se rechaza o se sufija, nunca se acepta.
- **No toques el orden ni `ForwardLimit` de `ForwardedHeaders`.** Es `ForwardLimit = 1` a propósito y va primero en el pipeline; de eso depende la IP real del rate limiting. Hoy no hay proxy delante (Cloudflare está en DNS only); si se activa la nube naranja o se agrega Front Door/CDN, todos compartirían el cupo de rate limiting y hay que revisarlo (ver `docs/CONTEXT.md` §6.9).
- **El hub se conecta con un ticket de 60 s, nunca con el JWT de sesión (12 h).** `POST /api/Hub/ticket` lo emite (verifica `EsActivo` en la base), `PedidosHub` exige el esquema `HubTicket` y el esquema por defecto no lee `access_token`. No aceptes el JWT de sesión por query string, no reutilices el `aud` de la API para el ticket (`JwtTokens` arma y valida los dos), no le pongas `[EnableRateLimiting]` al ticket (traba reconexiones) y no toques `CloseOnAuthenticationExpiration = false` (con `true` el panel se reconectaría cada minuto). No loguees `Request.QueryString` ni la URL completa, no saques el filtro de `Microsoft.AspNetCore.Hosting.Diagnostics` de `Program.cs` y no uses `UseHttpLogging`. Detalle y flujo en `docs/CONTEXT.md` §6.1.
- **Rate limiting: es por instancia y en memoria.** Con más de una instancia del App Service el límite efectivo se multiplica. Un endpoint público nuevo que escribe datos o es barato de abusar necesita `[EnableRateLimiting("...")]` explícito; no hay límite global. **El webhook de MP no debe limitarse** (perderías pagos).
- **Caché del menú:** si agregás una entidad que se muestra en el menú público, sumala a `MenuCacheInvalidationInterceptor.ResolverAdministradores`, o el menú queda viejo hasta 60 s. `ExecuteUpdate`/`ExecuteDelete`/SQL crudo **no pasan por SaveChanges**: invalidá a mano con `IMenuPublicoCache.Invalidar(adminId)` (ver `VarianteProductoRepository.EliminarTodas`). No llames `Invalidar` desde controllers para el caso normal.
- **El estado abierto/cerrado nunca va en el caché del menú.** El caché guarda datos de horario; el `EstadoLocal` se calcula en cada request (HIT y MISS) con `TimeProvider`. Cacheado, el local seguiría "abierto" hasta 60 s después de cerrar.
- **Reglas de horarios solo en `HorarioLocalValidator` / `HorarioLocalIntervalos`:** 0 o 7 días, máx. 2 turnos, cierre < apertura = turno nocturno, apertura inclusiva y cierre exclusivo, sin horarios = siempre abierto. El panel valida estricto; lo público **falla abierto** (`EstadoLocalPublico`: zona inválida o error de cálculo → abierto + `LogError`, nunca 500 ni cerrado).
- **Iniciar un pedido con el local cerrado = 409 `LocalCerrado`** (`AsegurarAbiertoAsync`). No bloquees `cupones/validar`, `preferencia-mp` ni `estado-pago`: no inician nada o operan sobre un pedido que ya existe.
- **No saques `local.esActivo` ni `local.horarios` del menú público** hasta que ningún front los lea (`esActivo` hoy significa "abierto ahora" por compatibilidad; `docs/CONTEXT.md` §6.13).
- **Migraciones en producción:** no hay `Database.Migrate()`. Se corre a mano un script idempotente en Azure (`SET XACT_ABORT ON` arriba, **sin** línea `GO`, backup previo, verificación después) y **antes** de desplegar el código que lo usa. Procedimiento en `docs/CONTEXT.md` §7.
- **Un 500 justo después de un push puede ser el despliegue en curso:** esperá el pipeline en verde y repetí la request antes de diagnosticar (`docs/CONTEXT.md` §9).
- **No asumas que el `CostoEnvio` es una columna:** va dentro de `Pedido.Total` y se reconstruye por resta en tres lugares con fórmulas distintas. Cualquier cambio en el cálculo de totales los afecta a todos. Tampoco asumas que `Subtotal` significa lo mismo en todos los DTOs de pedido (ver `docs/CONTEXT.md` §8).
- **`estado-pago` público devuelve solo 5 campos, sin datos personales.** No agregues nombre, teléfono, dirección ni detalle: el único secreto del endpoint es el código de seguimiento.
- **`FormaEntrega` del pedido público es whitelist** (`Delivery`, `Retira`) y nombre/teléfono son obligatorios. Si agregás un valor, actualizá la whitelist y todos los `== "Delivery"` (son case-sensitive).
- **`GET /api/Pedidos` es paginado** (`PagedResultDTO`, orden `Fecha DESC, Id DESC`, proyección en SQL). No vuelvas a cargar entidades con `Include` para listar.
- **No commitees `appsettings.json` ni `appsettings.Development.json`**, ni nada bajo `build_temp/`, `.vs/`, `obj/`, `bin/` o `uploads/`. Si falta una clave de configuración, agregá el placeholder en `appsettings.Example.json`.
- **No pongas secretos en código, docs ni ejemplos.** Referencialos por nombre de configuración. En producción (App Service Linux) el separador de sección es doble guion bajo: `JwtSettings__Key`.
- **No confíes en el payload del webhook de Mercado Pago sin validar la firma** (`MercadoPagoSignatureValidator`).
- **No agregues validación de configuración de MP en el constructor de `MercadoPagoService`:** se inyecta en `PedidosController`, y un throw ahí tumba pedidos que no usan MP. Validá al usar (`RequireConfig`).
- No existen y no hay que reintroducir: `CrearConDetalles`, `PedidoRequestDTO`, `ObtenerTodos` en pedidos, `DiagnosticoController`, el `PUT /api/Pedidos/{id}`.

## Cómo trabajar acá

1. **Leé el código real antes de tocarlo.** Este repo tiene convenciones que no se deducen del nombre de los archivos y patrones que conviven sin unificar.
2. **No agregues dependencias** salvo necesidad real; si hace falta una, decilo y justificala antes.
3. **Corré build y tests** después de cambiar código: `dotnet build Eat_Experience.sln` y `dotnet test Eat_Experience.sln` (xUnit; cubre solo la lógica pura de horarios y estado del local: no hay tests de pedidos, DB ni HTTP, y el pipeline no los corre). Si tocás horarios, estado del local, el menú público o el alta de pedido público, corré también `tools/verificar-horarios-local.ps1 -IniciarApi` (extremo a extremo, **solo contra API y SQL locales**; `docs/CONTEXT.md` §11).
4. **No hagas `git commit` ni `git push`.** Dejá los cambios en el árbol de trabajo; el commit lo decide quien te pidió el cambio.

---

Para detalle de arquitectura, flujos, decisiones y deuda conocida, leer `docs/CONTEXT.md`.
