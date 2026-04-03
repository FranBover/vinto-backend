namespace Eat_Experience.DTOs
{
    public class PedidoCreateResponseDTO
    {
        public int PedidoId { get; set; }
        public string CodigoSeguimiento { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal CostoEnvio { get; set; }
        public decimal Total { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string ResumenWhatsApp { get; set; } = string.Empty;
    }
}

