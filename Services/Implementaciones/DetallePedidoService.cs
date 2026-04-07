using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class DetallePedidoService : IDetallePedidoService
    {
        private readonly IDetallePedidoRepository _repository;

        public DetallePedidoService(IDetallePedidoRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<DetallePedido>> ObtenerTodos()
        {
            return await _repository.ObtenerTodos();
        }

        public async Task<DetallePedido?> ObtenerPorId(int id)
        {
            return await _repository.ObtenerPorId(id);
        }

        public async Task Crear(DetallePedido detallePedido)
        {
            await _repository.Crear(detallePedido);
        }

        public async Task Actualizar(DetallePedido detallePedido)
        {
            await _repository.Actualizar(detallePedido);
        }

        public async Task Eliminar(int id)
        {
            await _repository.Eliminar(id);
        }
    }
}
