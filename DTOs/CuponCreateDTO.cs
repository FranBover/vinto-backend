namespace Vinto.Api.DTOs
{
    public class CuponCreateDTO
    {
        public string Codigo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public int? LimiteUsos { get; set; }
        public decimal? PedidoMinimo { get; set; }
    }
}
