namespace Vinto.Api.DTOs
{
    public class ImagenUploadRequestDTO
    {
        public IFormFile File { get; set; } = null!;
        public string Tipo { get; set; } = string.Empty;
        public int? EntidadId { get; set; }
        public int Orden { get; set; } = 0;
    }
}
