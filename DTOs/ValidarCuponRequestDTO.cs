namespace Vinto.Api.DTOs
{
    public class ValidarCuponRequestDTO
    {
        public string Codigo { get; set; } = string.Empty;
        public decimal SubtotalPostDescuentos { get; set; }
    }
}
