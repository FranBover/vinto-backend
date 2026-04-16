using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.Models
{
    public class TipoVariante
    {
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; }

        public Producto Producto { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        public int Orden { get; set; } = 0;

        public ICollection<OpcionVariante> Opciones { get; set; } = new List<OpcionVariante>();
    }
}
