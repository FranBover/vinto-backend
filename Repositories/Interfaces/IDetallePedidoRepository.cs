using Eat_Experience.Models;

namespace Eat_Experience.Repositories.Interfaces
{
    public interface IDetallePedidoRepository
    {
        Task<IEnumerable<DetallePedido>> ObtenerTodos();
        Task<DetallePedido?> ObtenerPorId(int id);
        Task Crear(DetallePedido detallePedido);
        Task Actualizar(DetallePedido detallePedido);
        Task Eliminar(int id);
    }
}
