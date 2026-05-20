using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Vinto.Api.Helpers;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MercadoPagoController : ControllerBase
    {
        private readonly IMercadoPagoService _service;
        private readonly string _frontendAdminUrl;
        private readonly ILogger<MercadoPagoController> _logger;

        public MercadoPagoController(IMercadoPagoService service, IConfiguration configuration, ILogger<MercadoPagoController> logger)
        {
            _service = service;
            _frontendAdminUrl = configuration["MercadoPago:FrontendAdminUrl"]
                ?? throw new InvalidOperationException("MercadoPago:FrontendAdminUrl no configurado");
            _logger = logger;
        }

        // GET /api/mercadopago/oauth/url
        // Devuelve la URL a la que el frontend debe redirigir al admin para autorizar a Vinto en MP.
        [HttpGet("oauth/url")]
        public async Task<IActionResult> ObtenerUrlAutorizacion()
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (adminIdClaim == null || !int.TryParse(adminIdClaim, out int adminId))
                return Forbid();

            var result = await _service.GenerarUrlAutorizacion(adminId);
            return Ok(result);
        }

        // GET /api/mercadopago/oauth/callback
        // MP redirige acá tras la autorización. No requiere JWT porque MP no lo tiene.
        // La seguridad la da la validación del state contra el cache.
        [HttpGet("oauth/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery(Name = "error_description")] string? errorDescription)
        {
            // Caso 1: MP devolvió un error (ej: el admin rechazó autorizar)
            if (!string.IsNullOrEmpty(error))
            {
                var motivo = !string.IsNullOrEmpty(errorDescription) ? errorDescription : error;
                return Redirect($"{_frontendAdminUrl}?mp=denied&motivo={Uri.EscapeDataString(motivo)}");
            }

            // Caso 2: faltan parámetros obligatorios
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            {
                return Redirect($"{_frontendAdminUrl}?mp=error&motivo=missing_params");
            }

            // Caso 3: intentar procesar el callback
            try
            {
                await _service.ProcesarCallback(code, state);
                return Redirect($"{_frontendAdminUrl}?mp=success");
            }
            catch (ValidacionException ex)
            {
                return Redirect($"{_frontendAdminUrl}?mp=error&motivo={Uri.EscapeDataString(ex.Message)}");
            }
            catch (Exception)
            {
                return Redirect($"{_frontendAdminUrl}?mp=error&motivo=server_error");
            }
        }

        // POST /api/mercadopago/desconectar
        // Limpia todos los campos MP del admin.
        [HttpPost("desconectar")]
        public async Task<IActionResult> Desconectar()
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (adminIdClaim == null || !int.TryParse(adminIdClaim, out int adminId))
                return Forbid();

            await _service.Desconectar(adminId);
            return NoContent();
        }

        // GET /api/mercadopago/estado
        // Devuelve el estado de conexión MP del admin (para mostrar en la UI).
        [HttpGet("estado")]
        public async Task<IActionResult> ObtenerEstado()
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (adminIdClaim == null || !int.TryParse(adminIdClaim, out int adminId))
                return Forbid();

            var estado = await _service.ObtenerEstado(adminId);
            return Ok(estado);
        }

        // GET /api/mercadopago/diagnostico
        [HttpGet("diagnostico")]
        public async Task<IActionResult> Diagnostico()
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (adminIdClaim == null || !int.TryParse(adminIdClaim, out int adminId))
                return Forbid();

            try
            {
                var resultado = await _service.ObtenerDiagnostico(adminId);
                return Ok(resultado);
            }
            catch (ValidacionException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        // POST /api/mercadopago/webhook
        // Recibe notificaciones de pago de MercadoPago. Sin auth JWT (MP no lo tiene).
        // La seguridad la da la validación de firma HMAC-SHA256 en el service.
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                // Leer headers que MP envía para validar firma
                var xSignature = Request.Headers["x-signature"].FirstOrDefault() ?? "";
                var xRequestId = Request.Headers["x-request-id"].FirstOrDefault() ?? "";

                // Leer body
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                // Parsear body para extraer data.id (es el paymentId)
                string? paymentId = null;
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        var webhook = System.Text.Json.JsonSerializer.Deserialize<DTOs.MercadoPagoWebhookDTO>(body);
                        paymentId = webhook?.Data?.Id;
                    }
                    catch
                    {
                        // Body inválido — respondemos 200 igual para que MP no reintente.
                        return Ok();
                    }
                }

                if (!string.IsNullOrEmpty(paymentId))
                {
                    await _service.ProcesarWebhookPago(paymentId, xRequestId, xSignature);
                }

                // SIEMPRE respondemos 200 para que MP no reintente innecesariamente.
                // Los problemas se loggean internamente.
                return Ok();
            }
            catch (Exception ex)
            {
                // Solo en errores graves devolvemos 500 → MP va a reintentar más tarde.
                _logger.LogError(ex, "Excepción no manejada en el endpoint de webhook de MercadoPago.");
                return StatusCode(500);
            }
        }

        [HttpPost("dev/simular-webhook-aprobado")]
        [AllowAnonymous]
        public async Task<IActionResult> SimularWebhookAprobadoDev(
            [FromQuery] int pedidoId,
            [FromQuery] string paymentIdSimulado,
            [FromServices] IWebHostEnvironment env)
        {
            if (!env.IsDevelopment())
                return NotFound();

            try
            {
                var resultado = await _service.SimularWebhookAprobadoDev(pedidoId, paymentIdSimulado);
                return Ok(resultado);
            }
            catch (ValidacionException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }
    }
}
