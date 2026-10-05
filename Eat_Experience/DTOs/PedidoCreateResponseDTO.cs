namespace Vinto.Api.DTOs
{
    public class PedidoCreateResponseDTO
    {
        public int PedidoId { get; set; }
        public string CodigoSeguimiento { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public decimal SubtotalSinDescuentos { get; set; }
        public decimal MontoDescuentoProductos { get; set; }
        public decimal MontoDescuentoCupon { get; set; }
        public string? CodigoCupon { get; set; }
        public decimal Subtotal { get; set; }
        public decimal CostoEnvio { get; set; }
        public decimal Total { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string ResumenWhatsApp { get; set; } = string.Empty;
    }
}

