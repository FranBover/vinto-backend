namespace Vinto.Api.DTOs
{
    public class ComentarioPedidoResponseDTO
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
    }
}
