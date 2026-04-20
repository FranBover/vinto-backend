using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface IDescuentoRepository
    {
        Task<List<Descuento>> ObtenerPorAdminAsync(int adminId, bool? activo);
        Task<Descuento?> ObtenerPorIdAsync(int id, int adminId);
        Task<Descuento> CrearAsync(Descuento descuento);
        Task<Descuento> ActualizarAsync(Descuento descuento);
    }
}
