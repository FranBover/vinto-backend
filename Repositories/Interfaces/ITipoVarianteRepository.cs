using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface ITipoVarianteRepository
    {
        Task<IEnumerable<TipoVariante>> ObtenerPorProductoId(int productoId);
        Task<TipoVariante?> ObtenerPorId(int id);
        Task<int> ContarPorProductoId(int productoId);
        Task Crear(TipoVariante tipo);
        Task Actualizar(TipoVariante tipo);
        Task Eliminar(int id);
    }
}
