namespace Vinto.Api.DTOs
{
    public class ProductoUpdateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public string ImagenUrl { get; set; } = string.Empty;
        public bool Disponible { get; set; } = true;
        public int CategoriaId { get; set; }
    }
}

