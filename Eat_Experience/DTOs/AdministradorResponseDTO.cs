namespace Vinto.Api.DTOs
{
    // DTO de respuesta para Administrador. Expone todos los campos de la entidad EXCEPTO los
    // sensibles (PasswordHash y los tokens/credenciales de MercadoPago). El bool
    // MercadoPagoConectado SI se incluye porque no es secreto y la UI lo necesita.
    public class AdministradorResponseDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NombreLocal { get; set; } = string.Empty;
        public string SlugLocal { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string? LinkWhatsapp { get; set; }
        public string? LogoUrl { get; set; }
        public bool EsActivo { get; set; }
        public DateTime FechaRegistro { get; set; }
        public DateTime? UltimoAcceso { get; set; }
        public string? PlanSuscripcion { get; set; }
        public string? DominioPersonalizado { get; set; }
        public string? AliasTransferencia { get; set; }
        public string? TitularCuenta { get; set; }
        public string? Horarios { get; set; }
        public string? UbicacionUrl { get; set; }
        public string ZonaEnvio { get; set; } = "Nacional";
        public decimal? CostoEnvio { get; set; }
        public int? StockBajoAlerta { get; set; }
        public bool AutoDeshabilitarSinStock { get; set; }
        public DateTime? MercadoPagoTokenExpiresAt { get; set; }
        public bool MercadoPagoConectado { get; set; }
    }
}
