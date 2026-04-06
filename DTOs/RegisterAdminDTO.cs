using System.ComponentModel.DataAnnotations;

namespace Eat_Experience.DTOs
{
    public class RegisterAdminDTO
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string NombreLocal { get; set; } = string.Empty;

        [Required]
        public string Telefono { get; set; } = string.Empty;

        [Required]
        public string Direccion { get; set; } = string.Empty;
    }
}
