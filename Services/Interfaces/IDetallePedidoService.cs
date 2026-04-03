using Eat_Experience.Models;

namespace Eat_Experience.Services.Interfaces
{
    public interface IDetallePedidoService
    {
        Task<IEnumerable<DetallePedido>> ObtenerTodos();
        Task<DetallePedido?> ObtenerPorId(int id);
        Task Crear(DetallePedido detallePedido);
        Task Actualizar(DetallePedido detallePedido);
        Task Eliminar(int id);
    }
}
