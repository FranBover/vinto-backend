using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.DTOs
{
    public class VarianteProductoUpdateDTO
    {
        [Required, Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0.")]
        public decimal Precio { get; set; }

        public int? Stock { get; set; }

        [Required]
        public bool Disponible { get; set; }

        [MaxLength(100)]
        public string? Sku { get; set; }
    }
}
