using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IProductoExtraService
    {
        Task<IEnumerable<ProductoExtra>> ObtenerTodos();
        Task<IEnumerable<ProductoExtra>> ObtenerPorAdministradorId(int adminId);
        Task<ProductoExtra?> ObtenerPorId(int id);
        Task Crear(ProductoExtra extra);
        Task Actualizar(ProductoExtra extra);
        Task Eliminar(int id);
        Task<IEnumerable<ProductoExtra>> ObtenerPorProductoId(int productoId);
    }
}
