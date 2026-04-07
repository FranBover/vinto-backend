namespace Vinto.Api.Models
{
    public class DetallePedidoExtra
    {
        public int Id { get; set; }

        public int DetallePedidoId { get; set; }
        public DetallePedido DetallePedido { get; set; }

        public int ProductoExtraId { get; set; }
        public ProductoExtra ProductoExtra { get; set; }
    }
}
