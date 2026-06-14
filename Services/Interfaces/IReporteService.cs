using Vinto.Api.DTOs;

namespace Vinto.Api.Services.Interfaces
{
    public interface IReporteService
    {
        Task<DashboardReporteDTO> ObtenerDashboardAsync(int adminId, string periodo);
    }
}
