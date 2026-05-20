namespace Vinto.Api.DTOs
{
    public class CuponMetricasDTO
    {
        public int CuponId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public int UsosTotales { get; set; }
        public int UsosActivos { get; set; }
        public int UsosLiberados { get; set; }
        public decimal MontoTotalDescontado { get; set; }
        public decimal MontoTotalLiberado { get; set; }
        public DateTime? PrimerUso { get; set; }
        public DateTime? UltimoUso { get; set; }
    }
}
