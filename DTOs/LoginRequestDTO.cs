using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.DTOs
{
    public class LoginRequestDTO
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Contraseña { get; set; } = string.Empty;
    }
}
