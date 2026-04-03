using System;
using System.Collections.Generic;

namespace Eat_Experience.DTOs
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

        public decimal Total { get; set; }

        public List<PedidoDetalleResponseDTO> Detalles { get; set; } = new();
    }

    public class PedidoDetalleResponseDTO
    {
        public string NombreProducto { get; set; } = string.Empty;
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

