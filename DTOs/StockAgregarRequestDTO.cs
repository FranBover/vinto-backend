using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.DTOs
{
    public class StockAgregarRequestDTO
    {
        public int? VarianteId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0.")]
        public int Cantidad { get; set; }

        public string? Motivo { get; set; }
    }
}
