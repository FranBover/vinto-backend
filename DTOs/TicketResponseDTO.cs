namespace Vinto.Api.DTOs
{
    public class TicketResponseDTO
    {
        public int NumeroPedido { get; set; }
        public string CodigoSeguimiento { get; set; } = string.Empty;
        public string NombreLocal { get; set; } = string.Empty;
        public string TelefonoLocal { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public string TelefonoCliente { get; set; } = string.Empty;
        public string FormaEntrega { get; set; } = string.Empty;
        public string? DireccionCliente { get; set; }
        public string? ReferenciaDireccion { get; set; }
        public string FormaPago { get; set; } = string.Empty;
        public List<TicketItemDTO> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal CostoEnvio { get; set; }
        public decimal Total { get; set; }
        public decimal? MontoPagoEfectivo { get; set; }
        public decimal? Vuelto { get; set; }
    }

    public class TicketItemDTO
    {
        public string NombreProducto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public List<TicketExtraDTO> Extras { get; set; } = new();
    }

    public class TicketExtraDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioAdicional { get; set; }
    }
}
