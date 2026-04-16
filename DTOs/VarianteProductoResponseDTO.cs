namespace Vinto.Api.DTOs
{
    public class VarianteProductoResponseDTO
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public OpcionVarianteResponseDTO Opcion1 { get; set; } = null!;
        public OpcionVarianteResponseDTO? Opcion2 { get; set; }
        public decimal Precio { get; set; }
        public int? Stock { get; set; }
        public bool Disponible { get; set; }
        public string? Sku { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }
}
