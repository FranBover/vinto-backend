using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Vinto.Api.Models
{
    public class VarianteProducto
    {
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; }

        public Producto Producto { get; set; } = null!;

        [Required]
        public int Opcion1Id { get; set; }

        public OpcionVariante Opcion1 { get; set; } = null!;

        public int? Opcion2Id { get; set; }

        public OpcionVariante? Opcion2 { get; set; }

        [Precision(18, 2)]
        public decimal Precio { get; set; }

        public int? Stock { get; set; }

        public bool Disponible { get; set; } = true;

        [MaxLength(100)]
        public string? Sku { get; set; }
    }
}
