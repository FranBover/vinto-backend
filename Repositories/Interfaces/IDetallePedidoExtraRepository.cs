using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface IDetallePedidoExtraRepository
    {
        Task<IEnumerable<DetallePedidoExtra>> ObtenerPorDetallePedidoId(int detallePedidoId);
        Task Crear(DetallePedidoExtra detallePedidoExtra);
        Task EliminarPorDetallePedidoId(int detallePedidoId);
    }
}
