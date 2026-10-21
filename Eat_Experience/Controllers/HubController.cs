using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Controllers
{
    // Tickets para abrir la conexión de SignalR. El JWT de sesión ya no viaja en la URL del WebSocket:
    // el panel lo manda en el header de esta request y recibe a cambio un ticket de 60 s para el hub.
    // Sin [EnableRateLimiting] a propósito: requiere sesión y cada reconexión del panel pide uno (hasta
    // dos por intento, cada 5 s con la conexión caída); un límite por IP lo compartirían todos los
    // dispositivos de un local y trabaría justo las reconexiones. Ver docs/CONTEXT.md §6.1.
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HubController : ControllerBase
    {
        private readonly IHubTicketService _hubTicketService;

        public HubController(IHubTicketService hubTicketService)
        {
            _hubTicketService = hubTicketService;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        // POST api/Hub/ticket
        [HttpPost("ticket")]
        public async Task<IActionResult> EmitirTicket()
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var ticket = await _hubTicketService.EmitirAsync(adminId);
            if (ticket == null)
                return Unauthorized(new { mensaje = "Tu cuenta no está habilitada." });

            return Ok(ticket);
        }
    }
}
