namespace Vinto.Api.DTOs
{
    public class ProductoResponseDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public bool Disponible { get; set; }
        public int CategoriaId { get; set; }
        public int AdministradorId { get; set; }
        public List<ImagenResponseDTO> Imagenes { get; set; } = new();
    }
}

