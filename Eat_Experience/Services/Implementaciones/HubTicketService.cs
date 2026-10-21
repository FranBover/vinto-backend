using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class HubTicketService : IHubTicketService
    {
        private readonly IAdministradorRepository _administradorRepository;
        private readonly JwtSettings _jwtSettings;
        private readonly TimeProvider _timeProvider;

        public HubTicketService(IAdministradorRepository administradorRepository, JwtSettings jwtSettings, TimeProvider timeProvider)
        {
            _administradorRepository = administradorRepository;
            _jwtSettings = jwtSettings;
            _timeProvider = timeProvider;
        }

        public async Task<HubTicketResponseDTO?> EmitirAsync(int adminId)
        {
            // El JWT de sesión no se revisa contra la base al validarse: este es el punto donde una cuenta
            // deshabilitada deja de poder abrir (o reabrir) la conexión del hub.
            if (!await _administradorRepository.EstaHabilitadoAsync(adminId))
                return null;

            return new HubTicketResponseDTO
            {
                Ticket = JwtTokens.EmitirTicketHub(_jwtSettings, adminId, _timeProvider.GetUtcNow().UtcDateTime),
                ExpiraEnSegundos = JwtTokens.DuracionTicketSegundos
            };
        }
    }
}
