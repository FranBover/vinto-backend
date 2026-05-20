namespace Vinto.Api.DTOs
{
    public class CuponResponseDTO
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public DateTime? FechaVencimiento { get; set; }
        public int? LimiteUsos { get; set; }
        public int UsosActuales { get; set; }
        public decimal? PedidoMinimo { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
