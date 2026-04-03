using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Eat_Experience.Models
{
    public class Administrador
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string NombreLocal { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Direccion { get; set; } = string.Empty;

        [Required, Phone, MaxLength(20)]
        public string Telefono { get; set; } = string.Empty;

        [Url, MaxLength(300)]
        public string? LinkWhatsapp { get; set; }

        [Url, MaxLength(300)]
        public string? LogoUrl { get; set; }

        public bool EsActivo { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

        public DateTime? UltimoAcceso { get; set; }

        [MaxLength(50)]
        public string? PlanSuscripcion { get; set; }

        [MaxLength(100)]
        public string? DominioPersonalizado { get; set; }
    }
}
