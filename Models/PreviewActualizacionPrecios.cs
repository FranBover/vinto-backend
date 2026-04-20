using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class PreviewActualizacionPrecios
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public int AdministradorId { get; set; }

        [ForeignKey("AdministradorId")]
        public Administrador Administrador { get; set; } = null!;

        [Required]
        public int CategoriaId { get; set; }

        [ForeignKey("CategoriaId")]
        public Categoria Categoria { get; set; } = null!;

        [Required, MaxLength(20)]
        public string Tipo { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Valor { get; set; }

        public bool Redondear { get; set; } = true;

        [Required]
        public int TotalAfectados { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        [Required]
        public DateTime FechaExpiracion { get; set; }

        public bool Aplicado { get; set; } = false;

        public DateTime? FechaAplicacion { get; set; }

        public ICollection<PreviewActualizacionPreciosItem> Items { get; set; } = new List<PreviewActualizacionPreciosItem>();
    }
}
