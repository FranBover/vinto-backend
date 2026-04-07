using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IDetallePedidoExtraService
    {
        Task<IEnumerable<DetallePedidoExtra>> ObtenerPorDetallePedidoId(int detallePedidoId);
        Task Crear(DetallePedidoExtra detallePedidoExtra);
        Task EliminarPorDetallePedidoId(int detallePedidoId);
    }
}
