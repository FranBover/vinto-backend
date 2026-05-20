using System;
using System.Collections.Generic;

namespace Vinto.Api.DTOs
{
    public class PedidoDetailResponseDTO
    {
        public int Id { get; set; }
        public string Estado { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }

        public string NombreCliente { get; set; } = string.Empty;
        public string TelefonoCliente { get; set; } = string.Empty;

        public string FormaPago { get; set; } = string.Empty;
        public decimal? MontoPagoEfectivo { get; set; }
        public string FormaEntrega { get; set; } = string.Empty;

        public string? DireccionCliente { get; set; }
        public string? ReferenciaDireccion { get; set; }
        public string? UbicacionUrl { get; set; }

        public decimal SubtotalSinDescuentos { get; set; }
        public decimal MontoDescuentoProductos { get; set; }
        public decimal MontoDescuentoCupon { get; set; }
        public string? CodigoCupon { get; set; }
        public decimal Subtotal { get; set; }
        public decimal CostoEnvio { get; set; }
        public decimal Total { get; set; }

        public List<PedidoDetalleResponseDTO> Detalles { get; set; } = new();

        public string? MercadoPagoPaymentId { get; set; }
        public string? MercadoPagoStatus { get; set; }
        public string? MercadoPagoStatusDetail { get; set; }
        public DateTime? MercadoPagoFechaPago { get; set; }
        public string? MercadoPagoPreferenceId { get; set; }
        public string? MercadoPagoCollectionId { get; set; }
    }

    public class PedidoDetalleResponseDTO
    {
        public string NombreProducto { get; set; } = string.Empty;
        public string? VarianteDescripcion { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public List<PedidoDetalleExtraResponseDTO> Extras { get; set; } = new();
    }

    public class PedidoDetalleExtraResponseDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioAdicional { get; set; }
    }
}

