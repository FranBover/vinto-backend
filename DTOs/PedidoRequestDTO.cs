namespace Eat_Experience.DTOs
{
    public class PedidoRequestDTO
    {
        public int AdministradorId { get; set; }

        public string? NombreCliente { get; set; }

        public string? TelefonoCliente { get; set; }

        public string FormaPago { get; set; } // "Efectivo" o "MercadoPago"

        public string FormaEntrega { get; set; } // "Delivery" o "Retira"

        public decimal? MontoPagoEfectivo { get; set; }

        public string? DireccionCliente { get; set; }

        public List<DetallePedidoDTO> Detalles { get; set; }
    }




}

