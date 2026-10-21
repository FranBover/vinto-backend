using Vinto.Api.DTOs;

namespace Vinto.Api.Services.Interfaces
{
    public interface IHubTicketService
    {
        // null = el administrador no existe o su cuenta no está habilitada (EsActivo = false): no se emite ticket.
        Task<HubTicketResponseDTO?> EmitirAsync(int adminId);
    }
}
