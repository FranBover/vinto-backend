using System;

namespace Vinto.Api.DTOs
{
    public class PedidoListItemResponseDTO
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; } = string.Empty;

        public string NombreCliente { get; set; } = string.Empty;
        public string FormaPago { get; set; } = string.Empty;
        public string FormaEntrega { get; set; } = string.Empty;

        public decimal Total { get; set; }
        public int ItemsCount { get; set; }
    }
}

