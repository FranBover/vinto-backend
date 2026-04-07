namespace Vinto.Api.DTOs
{
    public class PedidoPublicCreateRequestDTO
    {
        public string? NombreCliente { get; set; }
        public string? TelefonoCliente { get; set; }
        public string FormaPago { get; set; } = string.Empty;
        public string FormaEntrega { get; set; } = string.Empty;
        public decimal? MontoPagoEfectivo { get; set; }
        public string? DireccionCliente { get; set; }
        public string? ReferenciaDireccion { get; set; }
        public string? UbicacionUrl { get; set; }
        public List<PedidoDetalleCreateDTO> Detalles { get; set; } = new();
    }
}

