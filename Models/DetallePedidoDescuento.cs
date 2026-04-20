using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class DetallePedidoDescuento
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PedidoId { get; set; }

        [ForeignKey("PedidoId")]
        public Pedido Pedido { get; set; } = null!;

        public int? DetallePedidoId { get; set; }

        [ForeignKey("DetallePedidoId")]
        public DetallePedido? DetallePedido { get; set; }

        public int? DescuentoId { get; set; }

        [ForeignKey("DescuentoId")]
        public Descuento? Descuento { get; set; }

        [Required, MaxLength(100)]
        public string NombreDescuentoSnapshot { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string TipoDescuento { get; set; } = string.Empty;

        [Required, Precision(18, 2)]
        public decimal MontoDescontado { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
