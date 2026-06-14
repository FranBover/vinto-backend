namespace Vinto.Api.DTOs
{
    public class DashboardReporteDTO
    {
        public RangoFechasDTO Periodo { get; set; } = null!;
        public RangoFechasDTO Comparacion { get; set; } = null!;
        public VentasResumenDTO Ventas { get; set; } = null!;
        public List<PuntoSerieDTO> SerieVentas { get; set; } = new();
        public List<TopProductoDTO> TopProductos { get; set; } = new();
        public List<TopCategoriaDTO> TopCategorias { get; set; } = new();
        public List<TopClienteDTO> TopClientes { get; set; } = new();
        public List<MetodoPagoDTO> MetodosPago { get; set; } = new();
        public List<HoraPicoDTO> HorasPico { get; set; } = new();
        public List<DiaPicoDTO> DiasPico { get; set; } = new();
    }

    public class RangoFechasDTO
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class VentasResumenDTO
    {
        public decimal Total { get; set; }
        public decimal TotalAnterior { get; set; }
        public decimal? VariacionPorcentual { get; set; }
        public int CantidadPedidos { get; set; }
        public int CantidadPedidosAnterior { get; set; }
        public decimal? VariacionPedidos { get; set; }
        public decimal TicketPromedio { get; set; }
        public decimal TicketPromedioAnterior { get; set; }
        public decimal? VariacionTicket { get; set; }
    }

    public class PuntoSerieDTO
    {
        public string Etiqueta { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public int Cantidad { get; set; }
    }

    public class TopProductoDTO
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Unidades { get; set; }
        public decimal Facturacion { get; set; }
    }

    public class TopCategoriaDTO
    {
        public int CategoriaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Unidades { get; set; }
        public decimal Facturacion { get; set; }
    }

    public class TopClienteDTO
    {
        public string NombreCliente { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public int CantidadPedidos { get; set; }
        public decimal Total { get; set; }
    }

    public class MetodoPagoDTO
    {
        public string FormaPago { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal Monto { get; set; }
        public decimal Porcentaje { get; set; }
    }

    public class HoraPicoDTO
    {
        public int Hora { get; set; }
        public int Cantidad { get; set; }
    }

    public class DiaPicoDTO
    {
        public int DiaSemana { get; set; }   // 0=Dom, 1=Lun, ..., 6=Sáb
        public int Cantidad { get; set; }
    }
}
