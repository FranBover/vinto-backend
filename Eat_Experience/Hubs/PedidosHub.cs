using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Vinto.Api.Helpers;

namespace Vinto.Api.Hubs
{
    // Solo acepta el ticket de 60 s de POST api/Hub/ticket; el JWT de sesion no sirve aca (ver JwtTokens).
    [Authorize(AuthenticationSchemes = JwtTokens.EsquemaHub)]
    public class PedidosHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // El grupo se deriva del token, no del cliente: cada conexi�n se suscribe SOLO
            // a su propio adminId. SignalR remueve la conexi�n de sus grupos al desconectarse.
            var adminId = Context.User?.FindFirst("adminId")?.Value;
            if (!string.IsNullOrEmpty(adminId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, adminId);
            }

            await base.OnConnectedAsync();
        }
    }
}
