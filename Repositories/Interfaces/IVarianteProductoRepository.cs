using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface IVarianteProductoRepository
    {
        Task<IEnumerable<VarianteProducto>> ObtenerPorProductoId(int productoId);
        Task<VarianteProducto?> ObtenerPorId(int id);
        Task CrearRango(IEnumerable<VarianteProducto> variantes);
        Task Actualizar(VarianteProducto variante);
        Task Eliminar(int id);
    }
}
