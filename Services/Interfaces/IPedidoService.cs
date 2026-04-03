using Eat_Experience.DTOs;
using Eat_Experience.Models;

namespace Eat_Experience.Services.Interfaces
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

       
    }
}
