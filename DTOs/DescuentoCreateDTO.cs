namespace Vinto.Api.DTOs
{
    public class DescuentoCreateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public int? ProductoId { get; set; }
        public int? CategoriaId { get; set; }
        public bool AplicaAPedidoCompleto { get; set; } = false;
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
    }
}
