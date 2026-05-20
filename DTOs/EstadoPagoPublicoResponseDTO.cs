namespace Vinto.Api.DTOs
{
    public class EstadoPagoPublicoResponseDTO
    {
        public bool Encontrado { get; set; }
        public string? Estado { get; set; }
        public string? MercadoPagoStatus { get; set; }
        public decimal? Total { get; set; }
        public string? ResumenWhatsApp { get; set; }
        public string? NombreCliente { get; set; }
        public string? LinkWhatsapp { get; set; }
    }
}
