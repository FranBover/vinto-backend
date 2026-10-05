using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface IPedidoRepository
    {
        Task<(List<PedidoListItemResponseDTO> Items, int Total)> ObtenerFiltradosPaginado(
            int adminId, string? estado, DateTime? desde, DateTime? hasta, string? formaPago, string? formaEntrega,
            int page, int pageSize);
        Task<Pedido?> ObtenerPorId(int id);
        Task Crear(Pedido pedido);
        Task Actualizar(Pedido pedido);
        Task Eliminar(int id);

        Task<IEnumerable<ComentarioPedido>?> GetComentariosAsync(int pedidoId, int adminId);
        Task AddComentarioAsync(ComentarioPedido comentario);

        Task<Pedido?> GetComandaAsync(int pedidoId, int adminId);
        Task<Pedido?> GetTicketAsync(int pedidoId, int adminId);
    }
}
