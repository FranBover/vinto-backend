using Vinto.Api.Models;

namespace Vinto.Api.Helpers
{
    // Única definición de qué intervalos cubre un HorarioLocal. La usan el cálculo del estado del local
    // y el validador, para que no puedan discrepar sobre la regla del turno nocturno.
    public static class HorarioLocalIntervalos
    {
        public static readonly TimeSpan UnDia = TimeSpan.FromHours(24);

        // Intervalos [Inicio, Fin) del día, como desplazamientos desde la medianoche de ese día.
        // Fin puede ser mayor a 24 h cuando el turno termina al día siguiente.
        // Un modo desconocido, "Cerrado" y los turnos incompletos o con apertura == cierre no aportan nada.
        public static IEnumerable<(TimeSpan Inicio, TimeSpan Fin)> DelDia(HorarioLocal horario)
        {
            switch (horario.Modo)
            {
                case ModoHorario.VeinticuatroHoras:
                    yield return (TimeSpan.Zero, UnDia);
                    break;

                case ModoHorario.Turnos:
                    if (Turno(horario.Turno1Apertura, horario.Turno1Cierre) is { } turno1)
                        yield return turno1;
                    if (Turno(horario.Turno2Apertura, horario.Turno2Cierre) is { } turno2)
                        yield return turno2;
                    break;
            }
        }

        // Cierre menor que apertura: el turno termina al día siguiente (ej. 20:00 a 01:00).
        // Apertura igual a cierre no es un turno (para las 24 horas existe el modo 24h).
        public static (TimeSpan Inicio, TimeSpan Fin)? Turno(TimeOnly? apertura, TimeOnly? cierre)
        {
            if (apertura is not { } desde || cierre is not { } hasta || desde == hasta)
                return null;

            var inicio = desde.ToTimeSpan();
            var fin = hasta.ToTimeSpan();
            return (inicio, fin > inicio ? fin : fin + UnDia);
        }
    }
}
