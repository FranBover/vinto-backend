namespace Vinto.Api.DTOs
{
    public class EstadoConexionMpResponseDTO
    {
        public bool Conectado { get; set; }
        public string? MercadoPagoUserId { get; set; }
        public DateTime? TokenExpiraEn { get; set; }
    }
}
