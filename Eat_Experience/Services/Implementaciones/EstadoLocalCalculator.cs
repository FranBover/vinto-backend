using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class EstadoLocalCalculator : IEstadoLocalCalculator
    {
        // El horario se repite cada semana: si en 7 días no hay ningún cambio, no habrá ninguno nunca.
        private const int DiasDeBusqueda = 7;

        private readonly record struct Tramo(DateTimeOffset Inicio, DateTimeOffset Fin);

        public EstadoLocal Calcular(IReadOnlyList<HorarioLocal> horarios, bool cerradoManualmente, string zonaHoraria, DateTimeOffset ahoraUtc)
        {
            if (cerradoManualmente)
                return new EstadoLocal(false, MotivoEstadoLocal.CerradoManualmente, null);

            if (horarios.Count == 0)
                return new EstadoLocal(true, MotivoEstadoLocal.SinHorarios, null);

            if (string.IsNullOrWhiteSpace(zonaHoraria) || !TimeZoneInfo.TryFindSystemTimeZoneById(zonaHoraria, out var zona))
                throw new ArgumentException($"La zona horaria '{zonaHoraria}' no es válida.", nameof(zonaHoraria));

            var tramos = ArmarTramos(horarios, zona, ahoraUtc);
            var limite = ahoraUtc.AddDays(DiasDeBusqueda);

            var actual = tramos.FindIndex(t => t.Inicio <= ahoraUtc && ahoraUtc < t.Fin);
            if (actual >= 0)
            {
                var cierre = tramos[actual].Fin;
                return new EstadoLocal(true, MotivoEstadoLocal.Horario, cierre <= limite ? cierre : null);
            }

            var siguiente = tramos.FindIndex(t => t.Inicio > ahoraUtc);
            var apertura = siguiente >= 0 ? tramos[siguiente].Inicio : (DateTimeOffset?)null;
            return new EstadoLocal(false, MotivoEstadoLocal.Horario, apertura <= limite ? apertura : null);
        }

        // Todos los tramos abiertos de ayer a una semana adelante, ya en UTC, ordenados y fusionados.
        // Ayer se incluye porque un turno nocturno de ayer puede seguir abierto hoy.
        private static List<Tramo> ArmarTramos(IReadOnlyList<HorarioLocal> horarios, TimeZoneInfo zona, DateTimeOffset ahoraUtc)
        {
            var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ahoraUtc, zona).DateTime);
            var tramos = new List<Tramo>();

            // +1 día de margen: con horario de verano, 7 días en UTC pueden caer un día después en hora local.
            for (var fecha = hoy.AddDays(-1); fecha <= hoy.AddDays(DiasDeBusqueda + 1); fecha = fecha.AddDays(1))
            {
                var dia = (int)fecha.DayOfWeek;
                var medianoche = fecha.ToDateTime(TimeOnly.MinValue);

                foreach (var horario in horarios.Where(h => h.DiaSemana == dia))
                {
                    foreach (var (inicio, fin) in HorarioLocalIntervalos.DelDia(horario))
                    {
                        var tramo = new Tramo(AUtc(medianoche + inicio, zona), AUtc(medianoche + fin, zona));
                        if (tramo.Fin > tramo.Inicio)
                            tramos.Add(tramo);
                    }
                }
            }

            return Fusionar(tramos);
        }

        // Une los tramos que se superponen o se tocan (cierre == apertura siguiente): el local no "cambia" a medianoche
        // si un turno nocturno termina cuando empieza el 24h del día siguiente.
        private static List<Tramo> Fusionar(List<Tramo> tramos)
        {
            var fusionados = new List<Tramo>();
            foreach (var tramo in tramos.OrderBy(t => t.Inicio))
            {
                if (fusionados.Count > 0 && tramo.Inicio <= fusionados[^1].Fin)
                {
                    if (tramo.Fin > fusionados[^1].Fin)
                        fusionados[^1] = fusionados[^1] with { Fin = tramo.Fin };
                }
                else
                {
                    fusionados.Add(tramo);
                }
            }
            return fusionados;
        }

        // Hora de pared del local -> instante UTC. En un salto de reloj (hora que no existe) o en una hora repetida,
        // TimeZoneInfo devuelve el offset estándar: la apertura de las 02:30 inexistente cae a las 03:30.
        // Es una convención; lo importante es que apertura, cierre y "ahora" salgan de los mismos instantes.
        private static DateTimeOffset AUtc(DateTime horaLocal, TimeZoneInfo zona)
        {
            var offset = zona.GetUtcOffset(horaLocal);
            return new DateTimeOffset(horaLocal, offset).ToUniversalTime();
        }
    }
}
