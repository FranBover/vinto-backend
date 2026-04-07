namespace Vinto.Api.DTOs
{
    public class PedidoDetalleCreateDTO
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
        public List<int>? ExtrasSeleccionados { get; set; }
    }
}

