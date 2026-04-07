using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class DetallePedidoExtraService : IDetallePedidoExtraService
    {
        private readonly IDetallePedidoExtraRepository _repository;

        public DetallePedidoExtraService(IDetallePedidoExtraRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<DetallePedidoExtra>> ObtenerPorDetallePedidoId(int detallePedidoId)
        {
            return await _repository.ObtenerPorDetallePedidoId(detallePedidoId);
        }

        public async Task Crear(DetallePedidoExtra detallePedidoExtra)
        {
            await _repository.Crear(detallePedidoExtra);
        }

        public async Task EliminarPorDetallePedidoId(int detallePedidoId)
        {
            await _repository.EliminarPorDetallePedidoId(detallePedidoId);
        }
    }
}
