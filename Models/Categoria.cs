using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.Models
{
    public class Categoria
    {
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        public int AdministradorId { get; set; }
        public Administrador Administrador { get; set; } = null!;

        public ICollection<Producto> Productos { get; set; }
    }
}
