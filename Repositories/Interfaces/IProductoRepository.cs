using Eat_Experience.Models;

namespace Eat_Experience.Repositories.Interfaces
{
    public interface IProductoRepository
    {
        Task<IEnumerable<Producto>> ObtenerTodos();
        Task<Producto?> ObtenerPorId(int id);
        Task Crear(Producto producto);
        Task Actualizar(Producto producto);
        Task Eliminar(int id);
    }
}
