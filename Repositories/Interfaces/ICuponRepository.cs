using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface ICuponRepository
    {
        Task<List<Cupon>> ObtenerPorAdminAsync(int administradorId, bool? activo = null);
        Task<Cupon?> ObtenerPorIdAsync(int id, int administradorId);
        Task<Cupon?> ObtenerPorCodigoAsync(string codigoUppercase, int administradorId);
        Task<Cupon> CrearAsync(Cupon cupon);
        Task<Cupon> ActualizarAsync(Cupon cupon);
        Task<CuponMetricasDTO> ObtenerMetricasAsync(int cuponId);
        Task<Administrador?> ObtenerAdminActivoPorSlugAsync(string slugNormalizado);
    }
}
