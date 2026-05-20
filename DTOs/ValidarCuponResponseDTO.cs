namespace Vinto.Api.DTOs
{
    public class ValidarCuponResponseDTO
    {
        public bool Valido { get; set; }
        public string? Codigo { get; set; }
        public string? Tipo { get; set; }
        public decimal? Valor { get; set; }
        public decimal? MontoDescuento { get; set; }
        public decimal? NuevoSubtotal { get; set; }
        public string? Motivo { get; set; }
    }
}
