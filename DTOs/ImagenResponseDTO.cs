namespace Vinto.Api.DTOs
{
    public class ImagenResponseDTO
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public int? EntidadId { get; set; }
        public int Orden { get; set; }
        public string NombreOriginal { get; set; } = string.Empty;
        public long TamanioBytes { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
