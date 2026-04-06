using Eat_Experience.Models;

namespace Eat_Experience.Repositories.Interfaces
{
    public interface IProductoRepository
    {
        Task<IEnumerable<Producto>> ObtenerTodos();
        Task<IEnumerable<Producto>> ObtenerPorAdministradorId(int adminId);
        Task<Producto?> ObtenerPorId(int id);
        Task Crear(Producto producto);
        Task Actualizar(Producto producto);
        Task Eliminar(int id);
    }
}
