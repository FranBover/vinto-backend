using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class HorarioLocalService : IHorarioLocalService
    {
        // Errores de SQL Server por clave duplicada (índice único / PK).
        private const int ErrorIndiceUnico = 2601;
        private const int ErrorClaveUnica = 2627;

        private readonly AppDbContext _context;
        private readonly IEstadoLocalCalculator _calculator;
        private readonly TimeProvider _timeProvider;

        public HorarioLocalService(AppDbContext context, IEstadoLocalCalculator calculator, TimeProvider timeProvider)
        {
            _context = context;
            _calculator = calculator;
            _timeProvider = timeProvider;
        }

        public async Task<HorariosLocalResponseDTO?> ObtenerAsync(int adminId)
        {
            var admin = await _context.Administradores
                .AsNoTracking()
                .Where(a => a.Id == adminId)
                .Select(a => new { a.ZonaHoraria, a.CerradoManualmente })
                .FirstOrDefaultAsync();

            if (admin == null)
                return null;

            var horarios = await CargarHorarios(adminId);
            return Armar(admin.ZonaHoraria, admin.CerradoManualmente, horarios);
        }

        public async Task<HorariosLocalResponseDTO?> ReemplazarAsync(int adminId, HorariosLocalUpdateDTO dto)
        {
            if (dto.Dias == null)
                throw new HorariosInvalidosException(new[] { "Tenés que enviar la lista de días (vacía si el local no tiene horarios fijos)." });

            var (nuevos, erroresDeFormato) = HorarioLocalMapper.AEntidades(dto.Dias);
            if (erroresDeFormato.Count > 0)
                throw new HorariosInvalidosException(erroresDeFormato);

            var errores = HorarioLocalValidator.Validar(nuevos, dto.ZonaHoraria);
            if (errores.Count > 0)
                throw new HorariosInvalidosException(errores);

            var admin = await _context.Administradores.FirstOrDefaultAsync(a => a.Id == adminId);
            if (admin == null)
                return null;

            // Change tracker (no ExecuteDelete): así MenuCacheInvalidationInterceptor ve los cambios e invalida el menú.
            var existentes = await _context.HorariosLocal.Where(h => h.AdministradorId == adminId).ToListAsync();
            _context.HorariosLocal.RemoveRange(existentes);

            foreach (var horario in nuevos)
                horario.AdministradorId = adminId;
            _context.HorariosLocal.AddRange(nuevos);

            admin.ZonaHoraria = dto.ZonaHoraria!;

            try
            {
                // Un solo SaveChanges = una transacción: los borrados, las altas y la zona se aplican juntos o ninguno.
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw ConflictoDeEdicion();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: ErrorIndiceUnico or ErrorClaveUnica })
            {
                throw ConflictoDeEdicion();
            }

            return Armar(admin.ZonaHoraria, admin.CerradoManualmente, await CargarHorarios(adminId));
        }

        public async Task<EstadoLocal?> FijarCierreManualAsync(int adminId, bool cerrado)
        {
            var admin = await _context.Administradores.FirstOrDefaultAsync(a => a.Id == adminId);
            if (admin == null)
                return null;

            admin.CerradoManualmente = cerrado;
            await _context.SaveChangesAsync();

            return Calcular(admin.ZonaHoraria, admin.CerradoManualmente, await CargarHorarios(adminId));
        }

        private async Task<List<HorarioLocal>> CargarHorarios(int adminId)
            => await _context.HorariosLocal
                .AsNoTracking()
                .Where(h => h.AdministradorId == adminId)
                .OrderBy(h => h.DiaSemana)
                .ToListAsync();

        private HorariosLocalResponseDTO Armar(string zonaHoraria, bool cerradoManualmente, List<HorarioLocal> horarios) => new()
        {
            ZonaHoraria = zonaHoraria,
            CerradoManualmente = cerradoManualmente,
            Dias = horarios.Select(HorarioLocalMapper.ADto).ToList(),
            Estado = Calcular(zonaHoraria, cerradoManualmente, horarios)
        };

        private EstadoLocal Calcular(string zonaHoraria, bool cerradoManualmente, List<HorarioLocal> horarios)
            => _calculator.Calcular(horarios, cerradoManualmente, zonaHoraria, _timeProvider.GetUtcNow());

        private static ValidacionException ConflictoDeEdicion()
            => new("Los horarios se modificaron desde otro lugar mientras guardabas. Volvé a cargarlos y reintentá.", 409);
    }
}
