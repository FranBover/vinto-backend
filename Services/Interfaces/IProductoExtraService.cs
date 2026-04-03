using Eat_Experience.Models;

namespace Eat_Experience.Services.Interfaces
{
    public interface IProductoExtraService
    {
        Task<IEnumerable<ProductoExtra>> ObtenerTodos();
        Task<ProductoExtra?> ObtenerPorId(int id);
        Task Crear(ProductoExtra extra);
        Task Actualizar(ProductoExtra extra);
        Task Eliminar(int id);
        Task<IEnumerable<ProductoExtra>> ObtenerPorProductoId(int productoId);
    }
}
