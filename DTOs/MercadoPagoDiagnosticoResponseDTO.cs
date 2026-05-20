namespace Vinto.Api.DTOs
{
    public class MercadoPagoDiagnosticoResponseDTO
    {
        public bool Conectado { get; set; }
        public bool TokenExpirado { get; set; }
        public int PedidosPendientesConMP { get; set; }
    }
}
