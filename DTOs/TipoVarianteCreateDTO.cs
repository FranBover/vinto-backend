using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.DTOs
{
    public class TipoVarianteCreateDTO
    {
        [Required, MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        public int Orden { get; set; } = 0;
    }
}
