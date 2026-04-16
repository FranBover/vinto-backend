using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface ITipoVarianteService
    {
        Task<IEnumerable<TipoVarianteResponseDTO>> ObtenerPorProductoId(int productoId);
        Task<TipoVariante?> ObtenerPorId(int id);
        Task<int> ContarPorProductoId(int productoId);
        Task Crear(TipoVariante tipo);
        Task Actualizar(TipoVariante tipo);
        Task Eliminar(int id, int productoId);
    }
}
