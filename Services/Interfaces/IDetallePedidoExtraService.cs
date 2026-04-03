using Eat_Experience.Models;

namespace Eat_Experience.Services.Interfaces
{
    public interface IDetallePedidoExtraService
    {
        Task<IEnumerable<DetallePedidoExtra>> ObtenerPorDetallePedidoId(int detallePedidoId);
        Task Crear(DetallePedidoExtra detallePedidoExtra);
        Task EliminarPorDetallePedidoId(int detallePedidoId);
    }
}
