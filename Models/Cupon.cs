using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class Cupon
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AdministradorId { get; set; }

        [ForeignKey("AdministradorId")]
        public Administrador Administrador { get; set; } = null!;

        [Required, MaxLength(30)]
        public string Codigo { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Tipo { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Valor { get; set; }

        public DateTime? FechaVencimiento { get; set; }

        public int? LimiteUsos { get; set; }

        public int UsosActuales { get; set; } = 0;

        [Precision(18, 2)]
        public decimal? PedidoMinimo { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
