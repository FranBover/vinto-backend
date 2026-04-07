using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IProductoService
    {
        Task<IEnumerable<Producto>> ObtenerTodos();
        Task<IEnumerable<Producto>> ObtenerPorAdministradorId(int adminId);
        Task<Producto?> ObtenerPorId(int id);
        Task Crear(Producto producto);
        Task Actualizar(Producto producto);
        Task Eliminar(int id);
    }
}
