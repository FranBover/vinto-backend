using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.DTOs
{
    public class OpcionVarianteCreateDTO
    {
        [Required, MaxLength(100)]
        public string Valor { get; set; } = string.Empty;

        public int Orden { get; set; } = 0;
    }
}
