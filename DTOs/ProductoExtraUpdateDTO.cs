namespace Eat_Experience.DTOs
{
    public class ProductoExtraUpdateDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioAdicional { get; set; }
        public int ProductoId { get; set; }
    }
}

