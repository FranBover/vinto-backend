using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.Models
{
    public class MovimientoStock
    {
        public int Id { get; set; }

        [Required]
        public int AdministradorId { get; set; }

        public Administrador Administrador { get; set; } = null!;

        [Required]
        public int ProductoId { get; set; }

        public Producto Producto { get; set; } = null!;

        public int? VarianteProductoId { get; set; }

        public VarianteProducto? VarianteProducto { get; set; }

        [Required, MaxLength(20)]
        public string Tipo { get; set; } = string.Empty; // "entrada", "salida", "ajuste"

        [Required]
        public int Cantidad { get; set; }

        [Required]
        public int StockAnterior { get; set; }

        [Required]
        public int StockNuevo { get; set; }

        [MaxLength(300)]
        public string? Motivo { get; set; }

        public DateTime FechaCreacion { get; set; }
    }
}
