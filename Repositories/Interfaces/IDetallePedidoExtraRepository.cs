using Eat_Experience.Models;

namespace Eat_Experience.Repositories.Interfaces
{
    public interface IDetallePedidoExtraRepository
    {
        Task<IEnumerable<DetallePedidoExtra>> ObtenerPorDetallePedidoId(int detallePedidoId);
        Task Crear(DetallePedidoExtra detallePedidoExtra);
        Task EliminarPorDetallePedidoId(int detallePedidoId);
    }
}
