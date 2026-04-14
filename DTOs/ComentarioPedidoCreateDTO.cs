using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.DTOs
{
    public class ComentarioPedidoCreateDTO
    {
        [Required, MaxLength(500)]
        public string Texto { get; set; } = string.Empty;
    }
}
