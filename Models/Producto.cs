using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Vinto.Api.Models
{
    public class Producto
    {
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        [Precision(18, 2)] // hasta 99999999.99
        public decimal Precio { get; set; }

        public string ImagenUrl { get; set; }

        public bool Disponible { get; set; } = true;

        public int CategoriaId { get; set; }

        public Categoria Categoria { get; set; } = null!;

        [Required]
        public int AdministradorId { get; set; }
        public Administrador Administrador { get; set; } = null!;

        public ICollection<ProductoExtra> Extras { get; set; }

        public bool TieneVariantes { get; set; } = false;

        public int? Stock { get; set; }

        public ICollection<TipoVariante> TiposVariante { get; set; } = new List<TipoVariante>();

        public ICollection<VarianteProducto> Variantes { get; set; } = new List<VarianteProducto>();
    }
}
