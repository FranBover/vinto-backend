using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vinto.Api.Controllers
{
    // TEMPORAL: sirve para verificar ForwardedHeaders en App Service. Eliminar despues de verificar.
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticoController : ControllerBase
    {
        [HttpGet("ip")]
        public IActionResult GetIp()
        {
            var headers = Request.Headers
                .Where(h => h.Key.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)
                         || h.Key.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase)
                         || h.Key.Equals("X-Client-IP", StringComparison.OrdinalIgnoreCase)
                         || h.Key.Equals("X-Azure-ClientIP", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            return Ok(new
            {
                remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                scheme = Request.Scheme,
                host = Request.Host.ToString(),
                headers
            });
        }
    }
}
