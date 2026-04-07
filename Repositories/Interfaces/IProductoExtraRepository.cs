using Vinto.Api.Models;


namespace Vinto.Api.Repositories.Interfaces
{
    public interface IProductoExtraRepository
    {
        Task<IEnumerable<ProductoExtra>> ObtenerTodos();
        Task<IEnumerable<ProductoExtra>> ObtenerPorAdministradorId(int adminId);
        Task<ProductoExtra?> ObtenerPorId(int id);
        Task<IEnumerable<ProductoExtra>> ObtenerPorProductoId(int productoId);
        Task Crear(ProductoExtra extra);
        Task Actualizar(ProductoExtra extra);
        Task Eliminar(int id);
    }
}
