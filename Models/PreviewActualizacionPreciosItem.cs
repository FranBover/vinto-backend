using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class PreviewActualizacionPreciosItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid PreviewId { get; set; }

        [ForeignKey("PreviewId")]
        public PreviewActualizacionPrecios Preview { get; set; } = null!;

        [Required]
        public int ProductoId { get; set; }

        [ForeignKey("ProductoId")]
        public Producto Producto { get; set; } = null!;

        [Required, Precision(18, 2)]
        public decimal PrecioActual { get; set; }

        [Required, Precision(18, 2)]
        public decimal PrecioNuevo { get; set; }
    }
}
