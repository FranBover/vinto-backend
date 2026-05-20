namespace Vinto.Api.DTOs
{
    public class LineaParaCalculo
    {
        public int ProductoId { get; set; }
        public int CategoriaId { get; set; }
        public decimal PrecioUnitario { get; set; }
        public int Cantidad { get; set; }
    }

    public class CalcularResultado
    {
        public List<LineaResultado> Lineas { get; set; } = new();
        public decimal SubtotalSinDescuentos { get; set; }
        public decimal MontoDescuentoProductos { get; set; }
        public decimal MontoDescuentoPedidoCompleto { get; set; }
        public decimal SubtotalFinal { get; set; }
        public List<DescuentoAplicadoGlobal> DescuentosPedidoCompletoAplicados { get; set; } = new();
    }

    public class LineaResultado
    {
        public int ProductoId { get; set; }
        public decimal PrecioUnitarioOriginal { get; set; }
        public decimal PrecioUnitarioConDescuento { get; set; }
        public decimal MontoDescontado { get; set; }
        public int Cantidad { get; set; }
        public List<DescuentoAplicadoLinea> DescuentosAplicados { get; set; } = new();
    }

    public class DescuentoAplicadoLinea
    {
        public int DescuentoId { get; set; }
        public string NombreDescuento { get; set; } = string.Empty;
        public string TipoAlcance { get; set; } = string.Empty;
        public decimal MontoDescontado { get; set; }
    }

    public class DescuentoAplicadoGlobal
    {
        public int DescuentoId { get; set; }
        public string NombreDescuento { get; set; } = string.Empty;
        public decimal MontoDescontado { get; set; }
    }
}
