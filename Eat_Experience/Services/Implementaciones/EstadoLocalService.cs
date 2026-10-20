using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class EstadoLocalService : IEstadoLocalService
    {
        private readonly AppDbContext _context;
        private readonly IEstadoLocalCalculator _calculator;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<EstadoLocalService> _logger;

        public EstadoLocalService(AppDbContext context, IEstadoLocalCalculator calculator, TimeProvider timeProvider, ILogger<EstadoLocalService> logger)
        {
            _context = context;
            _calculator = calculator;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public Task<EstadoLocal> ObtenerAsync(Administrador admin)
            => CalcularAsync(admin, _timeProvider.GetUtcNow());

        public async Task AsegurarAbiertoAsync(Administrador admin)
        {
            // Un único "ahora" para decidir y para redactar el mensaje: no puede decir "abre hoy" con otro instante.
            var ahora = _timeProvider.GetUtcNow();
            var estado = await CalcularAsync(admin, ahora);

            if (estado.EstaAbierto)
                return;

            throw new LocalCerradoException(
                LocalCerradoMensaje.Armar(estado, admin.ZonaHoraria, ahora),
                estado.ProximoCambio);
        }

        private async Task<EstadoLocal> CalcularAsync(Administrador admin, DateTimeOffset ahoraUtc)
        {
            // El cierre manual gana sobre todo: si está activo no hace falta leer los horarios.
            var horarios = admin.CerradoManualmente
                ? new List<HorarioLocal>()
                : await _context.HorariosLocal.AsNoTracking().Where(h => h.AdministradorId == admin.Id).ToListAsync();

            return EstadoLocalPublico.Calcular(_calculator, horarios, admin.CerradoManualmente, admin.ZonaHoraria, ahoraUtc, _logger, admin.Id);
        }
    }
}
