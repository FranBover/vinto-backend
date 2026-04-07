namespace Vinto.Api.DTOs
{
    public class DetallePedidoDTO
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }

        public List<int>? ExtrasSeleccionados { get; set; }
    }
}
