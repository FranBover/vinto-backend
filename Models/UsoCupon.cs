using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class UsoCupon
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CuponId { get; set; }

        [ForeignKey("CuponId")]
        public Cupon Cupon { get; set; } = null!;

        [Required]
        public int PedidoId { get; set; }

        [ForeignKey("PedidoId")]
        public Pedido Pedido { get; set; } = null!;

        [Required, Precision(18, 2)]
        public decimal MontoDescontado { get; set; }

        public DateTime FechaUso { get; set; } = DateTime.UtcNow;

        public bool Liberado { get; set; } = false;

        public DateTime? FechaLiberacion { get; set; }
    }
}
