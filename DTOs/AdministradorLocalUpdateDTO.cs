namespace Vinto.Api.DTOs
{
    public class AdministradorLocalUpdateDTO
    {
        public string? NombreLocal { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
        public string? LinkWhatsapp { get; set; }
        public string? LogoUrl { get; set; }
        public bool? EsActivo { get; set; }
        public string? AliasTransferencia { get; set; }
        public string? TitularCuenta { get; set; }
        public string? Horarios { get; set; }
        public string? UbicacionUrl { get; set; }
        public string? ZonaEnvio { get; set; }
        public decimal? CostoEnvio { get; set; }
    }
}
