namespace Vinto.Api.DTOs
{
    public class StockResponseDTO
    {
        public int ProductoId { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public bool TieneVariantes { get; set; }
        public int? StockProducto { get; set; }
        public List<VarianteStockDTO>? Variantes { get; set; }
        public List<MovimientoStockDTO> UltimosMovimientos { get; set; } = new();
    }

    public class VarianteStockDTO
    {
        public int VarianteId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public int? Stock { get; set; }
        public bool Disponible { get; set; }
    }

    public class MovimientoStockDTO
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public int StockAnterior { get; set; }
        public int StockNuevo { get; set; }
        public string? Motivo { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
