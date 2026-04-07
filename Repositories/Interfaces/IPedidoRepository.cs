using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface IPedidoRepository
    {
        Task<IEnumerable<Pedido>> ObtenerTodos();
        Task<Pedido?> ObtenerPorId(int id);
        Task Crear(Pedido pedido);
        Task Actualizar(Pedido pedido);
        Task Eliminar(int id);
    }
}
