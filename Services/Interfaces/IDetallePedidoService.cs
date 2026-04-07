using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
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
