namespace Vinto.Api.DTOs
{
    public class OpcionVarianteResponseDTO
    {
        public int Id { get; set; }
        public string Valor { get; set; } = string.Empty;
        public int Orden { get; set; }
        public int TipoVarianteId { get; set; }
    }
}
