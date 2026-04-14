using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.Models
{
    public class ComentarioPedido
    {
        public int Id { get; set; }

        public int PedidoId { get; set; }
        public Pedido Pedido { get; set; } = null!;

        [Required, MaxLength(500)]
        public string Texto { get; set; } = string.Empty;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public int AdministradorId { get; set; }
        public Administrador Administrador { get; set; } = null!;
    }
}
