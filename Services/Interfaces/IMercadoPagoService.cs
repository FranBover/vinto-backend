using Vinto.Api.DTOs;

namespace Vinto.Api.Services.Interfaces
{
    public interface IMercadoPagoService
    {
        Task<OAuthUrlResponseDTO> GenerarUrlAutorizacion(int adminId);
        Task<int> ProcesarCallback(string code, string state);
        Task Desconectar(int adminId);
        Task<EstadoConexionMpResponseDTO> ObtenerEstado(int adminId);
        Task<CrearPreferenciaResponseDTO> CrearPreferenciaPago(string slug, int pedidoId, string codigoSeguimiento);
        Task ProcesarWebhookPago(string paymentId, string requestId, string xSignature);
        Task<object> SimularWebhookAprobadoDev(int pedidoId, string paymentIdSimulado);
        Task<MercadoPagoDiagnosticoResponseDTO> ObtenerDiagnostico(int adminId);
    }
}
