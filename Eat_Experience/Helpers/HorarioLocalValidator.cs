using System.Globalization;
using Vinto.Api.Models;

namespace Vinto.Api.Helpers
{
    // Validación pura de los horarios que guarda el admin (sin base de datos ni HTTP). Devuelve todos los
    // errores juntos, en español, para mostrarlos de una vez; lista vacía = válido. Quien la use los convierte
    // en ValidacionException. Se aceptan 0 días (local sin horarios fijos: siempre abierto) o exactamente 7.
    public static class HorarioLocalValidator
    {
        private const int DiasDeLaSemana = 7;

        private static readonly string[] NombresDias = { "domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado" };
        private static readonly TimeSpan Semana = TimeSpan.FromDays(DiasDeLaSemana);

        // "lunes", "martes"... para armar mensajes. Un número fuera de 0-6 se nombra como "día N".
        public static string NombreDelDia(int diaSemana)
            => diaSemana is >= 0 and <= 6 ? NombresDias[diaSemana] : $"día {diaSemana}";

        // Coincide con Administrador.ZonaHoraria (nvarchar(64)); además evita buscar en disco ids absurdos.
        private const int ZonaMaxLength = 64;

        // Un tramo abierto dentro de la semana (desde el domingo 00:00), para detectar superposiciones.
        private sealed record Tramo(string Descripcion, TimeSpan Inicio, TimeSpan Fin);

        public static IReadOnlyList<string> Validar(IReadOnlyList<HorarioLocal> horarios, string? zonaHoraria)
        {
            var errores = new List<string>();

            ValidarZona(zonaHoraria, errores);

            if (horarios.Count != 0 && horarios.Count != DiasDeLaSemana)
                errores.Add($"Los horarios tienen que cargar los 7 días de la semana (o ninguno, si el local no tiene horarios fijos): se recibieron {horarios.Count}.");

            var repetidos = horarios
                .Where(h => h.DiaSemana is >= 0 and <= 6)
                .GroupBy(h => h.DiaSemana)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .Order()
                .ToList();
            foreach (var dia in repetidos)
                errores.Add($"Hay más de un horario para el {NombresDias[dia]}: solo se permite uno por día.");

            var tramos = new List<Tramo>();
            foreach (var horario in horarios)
            {
                if (horario.DiaSemana is < 0 or > 6)
                {
                    AgregarUnico(errores, $"El día de la semana {horario.DiaSemana} no es válido: tiene que estar entre 0 (domingo) y 6 (sábado).");
                    continue;
                }

                var dia = NombresDias[horario.DiaSemana];
                var erroresAntes = errores.Count;
                ValidarDia(horario, dia, errores);

                // Solo los días sin errores propios entran al chequeo de superposición: evita errores en cascada.
                if (errores.Count == erroresAntes && !repetidos.Contains(horario.DiaSemana))
                    tramos.AddRange(TramosDe(horario, dia));
            }

            errores.AddRange(Superposiciones(tramos));
            return errores;
        }

        private static void ValidarZona(string? zonaHoraria, List<string> errores)
        {
            if (string.IsNullOrWhiteSpace(zonaHoraria))
                errores.Add("La zona horaria es obligatoria.");
            else if (zonaHoraria.Length > ZonaMaxLength || !TimeZoneInfo.TryFindSystemTimeZoneById(zonaHoraria, out _))
                errores.Add($"La zona horaria '{zonaHoraria}' no es válida. Usá un identificador como America/Argentina/Buenos_Aires.");
        }

        private static void ValidarDia(HorarioLocal horario, string dia, List<string> errores)
        {
            switch (horario.Modo)
            {
                case ModoHorario.Cerrado:
                case ModoHorario.VeinticuatroHoras:
                    if (horario.Turno1Apertura != null || horario.Turno1Cierre != null
                        || horario.Turno2Apertura != null || horario.Turno2Cierre != null)
                        errores.Add($"El {dia} está en modo {horario.Modo} y no puede tener turnos cargados: quitá los horarios o cambiá el modo a Turnos.");
                    break;

                case ModoHorario.Turnos:
                    ValidarTurnos(horario, dia, errores);
                    break;

                default:
                    errores.Add($"El modo '{horario.Modo}' del {dia} no es válido. Los valores permitidos son: {string.Join(", ", ModoHorario.Validos)}.");
                    break;
            }
        }

        private static void ValidarTurnos(HorarioLocal horario, string dia, List<string> errores)
        {
            var turno1Vacio = horario.Turno1Apertura == null && horario.Turno1Cierre == null;
            var turno2Vacio = horario.Turno2Apertura == null && horario.Turno2Cierre == null;

            if (turno1Vacio)
            {
                errores.Add(turno2Vacio
                    ? $"El {dia} está en modo Turnos pero no tiene el turno 1 cargado."
                    : $"El {dia} está en modo Turnos pero no tiene el turno 1 cargado: el turno 2 no puede usarse sin el turno 1.");
                return;
            }

            var erroresAntes = errores.Count;
            ValidarTurno(1, horario.Turno1Apertura, horario.Turno1Cierre, dia, errores);
            if (!turno2Vacio)
                ValidarTurno(2, horario.Turno2Apertura, horario.Turno2Cierre, dia, errores);

            // El orden solo se puede chequear si hay dos turnos y cada uno es válido por sí mismo.
            if (!turno2Vacio && errores.Count == erroresAntes)
                ValidarOrden(horario, dia, errores);
        }

        // Con dos turnos, el turno 1 es el primero del día: tiene que terminar el mismo día y el turno 2 empezar
        // a partir de su cierre. Solo el último turno del día (el 2) puede cruzar la medianoche.
        private static void ValidarOrden(HorarioLocal horario, string dia, List<string> errores)
        {
            var apertura1 = horario.Turno1Apertura!.Value;
            var cierre1 = horario.Turno1Cierre!.Value;
            var apertura2 = horario.Turno2Apertura!.Value;
            var cierre2 = horario.Turno2Cierre!.Value;

            if (cierre1 < apertura1)
                errores.Add($"El turno 1 del {dia} ({Hora(apertura1)} a {Hora(cierre1)}) cruza la medianoche, y solo el último turno del día puede hacerlo: terminalo antes de las 00:00 o cargalo como turno 2.");
            else if (apertura2 < cierre1)
                errores.Add($"El turno 2 del {dia} ({Hora(apertura2)} a {Hora(cierre2)}) tiene que empezar a partir del cierre del turno 1 ({Hora(cierre1)}).");
        }

        private static void ValidarTurno(int numero, TimeOnly? apertura, TimeOnly? cierre, string dia, List<string> errores)
        {
            if (apertura == null || cierre == null)
                errores.Add($"El turno {numero} del {dia} está incompleto: tiene que tener apertura y cierre.");
            else if (apertura == cierre)
                errores.Add($"El turno {numero} del {dia} abre y cierra a la misma hora ({Hora(apertura.Value)}). Si el local atiende las 24 horas, usá el modo 24h.");
        }

        // Los tramos de un día ya validado, con su posición en la semana. Un turno nocturno termina más allá
        // de las 24 h de su día y por eso puede pisar al día siguiente.
        private static IEnumerable<Tramo> TramosDe(HorarioLocal horario, string dia)
        {
            var inicioDelDia = TimeSpan.FromDays(horario.DiaSemana);

            if (horario.Modo == ModoHorario.VeinticuatroHoras)
            {
                yield return new Tramo($"el {dia} (24h)", inicioDelDia, inicioDelDia + HorarioLocalIntervalos.UnDia);
                yield break;
            }

            var turnos = new[]
            {
                (Numero: 1, Apertura: horario.Turno1Apertura, Cierre: horario.Turno1Cierre),
                (Numero: 2, Apertura: horario.Turno2Apertura, Cierre: horario.Turno2Cierre)
            };

            foreach (var (numero, apertura, cierre) in turnos)
            {
                if (HorarioLocalIntervalos.Turno(apertura, cierre) is not { } intervalo)
                    continue;

                var nocturno = intervalo.Fin > HorarioLocalIntervalos.UnDia;
                var diaSiguiente = NombresDias[(horario.DiaSemana + 1) % 7];
                var rango = $"{Hora(apertura!.Value)} a {Hora(cierre!.Value)}" + (nocturno ? $", termina el {diaSiguiente}" : "");
                yield return new Tramo($"el turno {numero} del {dia} ({rango})", inicioDelDia + intervalo.Inicio, inicioDelDia + intervalo.Fin);
            }
        }

        // La semana es circular: lo que desborda el sábado cae sobre el domingo. Se compara cada par de tramos
        // tal cual y corrido una semana hacia cada lado. Que un cierre coincida con una apertura no es superponerse.
        // El mensaje nombra primero el tramo que empieza antes en el solapamiento real (el sábado nocturno antes
        // que el domingo), no el que cae antes en la semana que arranca el domingo.
        private static IEnumerable<string> Superposiciones(List<Tramo> tramos)
        {
            for (var i = 0; i < tramos.Count; i++)
            {
                for (var j = i + 1; j < tramos.Count; j++)
                {
                    foreach (var corrimiento in new[] { TimeSpan.Zero, Semana, -Semana })
                    {
                        if (!SePisan(tramos[i], tramos[j], corrimiento))
                            continue;

                        var (primero, segundo) = tramos[i].Inicio <= tramos[j].Inicio + corrimiento
                            ? (tramos[i], tramos[j])
                            : (tramos[j], tramos[i]);
                        yield return $"Se superponen {primero.Descripcion} y {segundo.Descripcion}.";
                        break;
                    }
                }
            }
        }

        private static bool SePisan(Tramo a, Tramo b, TimeSpan corrimientoDeB)
            => a.Inicio < b.Fin + corrimientoDeB && b.Inicio + corrimientoDeB < a.Fin;

        private static void AgregarUnico(List<string> errores, string mensaje)
        {
            if (!errores.Contains(mensaje))
                errores.Add(mensaje);
        }

        private static string Hora(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
