namespace Vinto.Api.DTOs
{
    public class ComandaResponseDTO
    {
        public int NumeroPedido { get; set; }
        public string CodigoSeguimiento { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public string FormaEntrega { get; set; } = string.Empty;
        public string NombreCliente { get; set; } = string.Empty;
        public string? DireccionCliente { get; set; }
        public string? ReferenciaDireccion { get; set; }
        public List<ComandaItemDTO> Items { get; set; } = new();
    }

    public class ComandaItemDTO
    {
        public string NombreProducto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public List<string> Extras { get; set; } = new();
    }
}
