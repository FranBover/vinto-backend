namespace Vinto.Api.DTOs
{
    public class CategoriaResponseDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public string? ImagenUrl { get; set; }
    }
}
