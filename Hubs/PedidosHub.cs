using Microsoft.AspNetCore.SignalR;

namespace Vinto.Api.Hubs
{
    public class PedidosHub : Hub
    {
        public async Task JoinAdminGroup(string adminId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, adminId);
        }

        public async Task LeaveAdminGroup(string adminId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, adminId);
        }
    }
}
