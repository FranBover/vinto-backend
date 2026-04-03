using Eat_Experience.Models;

namespace Eat_Experience.Services.Interfaces
{
    public interface IProductoService
    {
        Task<IEnumerable<Producto>> ObtenerTodos();
        Task<Producto?> ObtenerPorId(int id);
        Task Crear(Producto producto);
        Task Actualizar(Producto producto);
        Task Eliminar(int id);
    }
}
