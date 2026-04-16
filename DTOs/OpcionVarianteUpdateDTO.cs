using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.DTOs
{
    public class OpcionVarianteUpdateDTO
    {
        [Required, MaxLength(100)]
        public string Valor { get; set; } = string.Empty;

        public int Orden { get; set; }
    }
}
