using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Vinto.Api.Models
{
    public class DetallePedido
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PedidoId { get; set; }

        [ForeignKey("PedidoId")]
        public Pedido Pedido { get; set; } = null!;

        [Required]
        public int ProductoId { get; set; }


        [ForeignKey("ProductoId")]
        [Required]
        public Producto Producto { get; set; } = null!;

        [Required]
        public int Cantidad { get; set; }


        [Required, Precision(18, 2)]
        public decimal PrecioUnitario { get; set; }

        public int? VarianteProductoId { get; set; }

        public VarianteProducto? VarianteProducto { get; set; }

        // Opcional: permitir agregar extras en cada producto del pedido
        public ICollection<DetallePedidoExtra> ProductosExtra { get; set; } = new List<DetallePedidoExtra>();
    }
}
