namespace Vinto.Api.DTOs
{
    public class StockAlertaDTO
    {
        public int ProductoId { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public int? VarianteId { get; set; }
        public string? VarianteDescripcion { get; set; }
        public int StockActual { get; set; }
        public string Tipo { get; set; } = string.Empty; // "agotado" | "bajo"
    }
}
