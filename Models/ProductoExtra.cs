using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Eat_Experience.Models
{
    public class ProductoExtra
    {
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; }  // ej: "Queso", "Sin tomate", "Extra bacon"

        [Precision(18, 2)]
        public decimal PrecioAdicional { get; set; }

        // Relación con Producto
        public int ProductoId { get; set; }

        public Producto Producto { get; set; } = null!;
    }
}
