namespace Vinto.Api.DTOs
{
    public class TipoVarianteResponseDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public int CantidadOpciones { get; set; }
    }
}
