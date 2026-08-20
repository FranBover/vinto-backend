# Vinto — guía para Claude Code

Vinto es un SaaS multi-tenant de tienda online para negocios chicos: cada negocio tiene su catálogo, su panel privado y su tienda pública sin login del comprador.
Este repo es el backend: **ASP.NET Core .NET 9 + EF Core 9 (SQL Server) + JWT + SignalR**, con Mercado Pago, Azure Blob Storage e ImageSharp.

La solución `Eat_Experience.sln` está en la raíz; el proyecto es `Eat_Experience/Vinto.Api.csproj` (la carpeta conserva el nombre viejo, el proyecto y el namespace ya son `Vinto.Api`).

## Convenciones no negociables

- **DTOs siempre en el borde HTTP.** Los controllers reciben y devuelven tipos de `DTOs/` (`*CreateDTO`, `*UpdateDTO`, `*ResponseDTO`, `*RequestDTO`). Nunca exponer ni aceptar entidades EF de `Models/` en un endpoint.
- **`adminId` sale del JWT, nunca del request.** El patrón es `TryGetAdminId(out int adminId)` leyendo el claim `adminId`; si no está, `Forbid()`. Ningún DTO de entrada lleva `AdministradorId`.
- **Aislamiento manual por tenant.** Los `HasQueryFilter` están comentados en `AppDbContext`. Toda consulta filtra `AdministradorId` a mano, y toda operación sobre una entidad existente valida pertenencia antes de tocarla (`if (x.AdministradorId != adminId) return Forbid();`). Un olvido es fuga entre tenants.
- **Capas:** `Controllers/` (HTTP, adminId, mapeo a DTO) → `Services/` (`Interfaces/` + `Implementaciones/`, una interfaz por servicio) → `Repositories/` / `AppDbContext`. La capa de repos es parcial y convive con acceso directo al contexto: seguí el estilo del archivo que estés tocando.
- **Nomenclatura en español** para entidades, servicios y propiedades (`Producto`, `PedidoService`, `NombreLocal`). Mantenerla.
- **Errores de negocio** con `ValidacionException(mensaje, statusCode)`; el middleware global de `Program.cs` los traduce. No armar respuestas de error ad hoc.
- **Plata en `decimal` con `HasPrecision(18, 2)`** configurado en `AppDbContext`. Toda columna monetaria nueva va igual.

## No hagas esto

- **No renombres** la carpeta `Eat_Experience/`, ni `Administrador.NombreLocal`, ni la ruta `/locales/{slug}`, ni el endpoint `comanda`. Son restos del origen del proyecto y romperlos rompe el frontend o fuerza migraciones.
- **No toques `Response.Clear()`** ni muevas el middleware de excepciones por debajo de `UseCors`: está arriba a propósito para no borrar los headers CORS y que un 500 no se reporte como error de CORS.
- **No introduzcas un tercer algoritmo de slug.** Ya hay dos incompatibles (uno en SQL sin quitar acentos, otro en memoria con `Slugify()`), y eso es un bug conocido. Si tocás slugs, arreglá la divergencia, no la ampliés.
- **No asumas que el `CostoEnvio` es una columna:** va dentro de `Pedido.Total` y se reconstruye por resta en tres lugares con fórmulas distintas. Cualquier cambio en el cálculo de totales los afecta a todos.
- **No commitees `appsettings.json` ni `appsettings.Development.json`**, ni nada bajo `build_temp/`, `.vs/`, `obj/`, `bin/` o `uploads/`. Si falta una clave de configuración, agregá el placeholder en `appsettings.Example.json`.
- **No pongas secretos en código, docs ni ejemplos.** Referencialos por nombre de configuración. En producción (App Service Linux) el separador de sección es doble guion bajo: `JwtSettings__Key`.
- **No confíes en el payload del webhook de Mercado Pago sin validar la firma** (`MercadoPagoSignatureValidator`).

## Cómo trabajar acá

1. **Leé el código real antes de tocarlo.** Este repo tiene convenciones que no se deducen del nombre de los archivos y patrones que conviven sin unificar.
2. **No agregues dependencias** salvo necesidad real; si hace falta una, decilo y justificala antes.
3. **Corré el build** después de cambiar código: `dotnet build Eat_Experience.sln`. No hay tests en la solución, así que el build es la única red.
4. **No hagas `git commit` ni `git push`.** Dejá los cambios en el árbol de trabajo; el commit lo decide quien te pidió el cambio.

---

Para detalle de arquitectura, flujos y decisiones, leer `docs/CONTEXT.md`.
