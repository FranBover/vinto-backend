using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IEstadoLocalCalculator
    {
        /// <summary>
        /// Calcula si un local está abierto en <paramref name="ahoraUtc"/>. No accede a base de datos ni al reloj:
        /// quien lo llama obtiene el instante de un <see cref="TimeProvider"/>.
        /// <list type="bullet">
        /// <item>Cerrado manualmente gana sobre todo. Sin filas de horario, el local está siempre abierto.</item>
        /// <item>Los días sin fila se consideran cerrados. La apertura es inclusiva y el cierre exclusivo.</item>
        /// <item>El día de la semana se evalúa en <paramref name="zonaHoraria"/>, no en UTC.</item>
        /// <item>Un turno con cierre menor que la apertura termina al día siguiente, y eso se tiene en cuenta aunque el día siguiente figure cerrado.</item>
        /// </list>
        /// Lanza <see cref="ArgumentException"/> si hay horarios y la zona horaria no es válida.
        /// </summary>
        EstadoLocal Calcular(IReadOnlyList<HorarioLocal> horarios, bool cerradoManualmente, string zonaHoraria, DateTimeOffset ahoraUtc);
    }
}
