using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class Descuento
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AdministradorId { get; set; }

        [ForeignKey("AdministradorId")]
        public Administrador Administrador { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Tipo { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Valor { get; set; }

        public int? ProductoId { get; set; }

        [ForeignKey("ProductoId")]
        public Producto? Producto { get; set; }

        public int? CategoriaId { get; set; }

        [ForeignKey("CategoriaId")]
        public Categoria? Categoria { get; set; }

        public bool AplicaAPedidoCompleto { get; set; } = false;

        public DateTime? FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
