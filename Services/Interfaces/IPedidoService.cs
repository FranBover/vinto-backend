using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IPedidoService
    {
        Task<IEnumerable<Pedido>> ObtenerTodos();
        Task<Pedido?> ObtenerPorId(int id);
        Task Crear(Pedido pedido);
        Task Actualizar(Pedido pedido);
        Task Eliminar(int id);


        Task<Pedido> CrearConDetalles(PedidoRequestDTO request);
        Task<PedidoCreateResponseDTO> CrearPublicoPorSlug(string slug, PedidoPublicCreateRequestDTO request);
        Task<string?> ObtenerResumenWhatsAppAdmin(int pedidoId, int adminId);

        Task<IEnumerable<Pedido>> ObtenerFiltrados(int adminId, string? estado, DateTime? desde, DateTime? hasta, string? formaPago, string? formaEntrega);
        Task<IEnumerable<ComentarioPedido>?> GetComentariosAsync(int pedidoId, int adminId);
        Task<ComentarioPedido?> AddComentarioAsync(int pedidoId, int adminId, string texto);
    }
}
