using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.DTOs
{
    public class StockAjustarRequestDTO
    {
        public int? VarianteId { get; set; }

        [Range(0, int.MaxValue)]
        public int NuevoStock { get; set; }

        public string? Motivo { get; set; }
    }
}
