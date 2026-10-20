using System.Globalization;
using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Helpers
{
    // Conversión pura entre HorarioDiaDTO (horas como "HH:mm") y HorarioLocal (TimeOnly).
    public static class HorarioLocalMapper
    {
        private static readonly string[] FormatosDeHora = { "HH:mm", "H:mm", "HH:mm:ss" };

        // Vacío o solo espacios = sin valor. Un texto que no es una hora devuelve false.
        public static bool TryParsearHora(string? texto, out TimeOnly? hora)
        {
            hora = null;
            if (string.IsNullOrWhiteSpace(texto))
                return true;

            if (!TimeOnly.TryParseExact(texto.Trim(), FormatosDeHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parseada))
                return false;

            hora = parseada;
            return true;
        }

        public static string? Formatear(TimeOnly? hora)
            => hora?.ToString("HH:mm", CultureInfo.InvariantCulture);

        // Devuelve las entidades (sin AdministradorId: lo asigna quien guarda) y los errores de formato de hora.
        // Si hay errores de formato las entidades no sirven para validar: la hora mal escrita quedaría vacía.
        public static (List<HorarioLocal> Horarios, List<string> Errores) AEntidades(IEnumerable<HorarioDiaDTO> dias)
        {
            var horarios = new List<HorarioLocal>();
            var errores = new List<string>();

            foreach (var dia in dias)
            {
                // El JSON puede traer "dias": [null], que el tipo no anticipa.
                if (dia is null)
                {
                    errores.Add("La lista de días tiene un elemento vacío.");
                    continue;
                }

                var nombreDia = HorarioLocalValidator.NombreDelDia(dia.DiaSemana);
                var horario = new HorarioLocal { DiaSemana = dia.DiaSemana, Modo = dia.Modo ?? string.Empty };

                horario.Turno1Apertura = Parsear(dia.Turno1Apertura, "la apertura del turno 1", nombreDia, errores);
                horario.Turno1Cierre = Parsear(dia.Turno1Cierre, "el cierre del turno 1", nombreDia, errores);
                horario.Turno2Apertura = Parsear(dia.Turno2Apertura, "la apertura del turno 2", nombreDia, errores);
                horario.Turno2Cierre = Parsear(dia.Turno2Cierre, "el cierre del turno 2", nombreDia, errores);

                horarios.Add(horario);
            }

            return (horarios, errores);
        }

        public static HorarioDiaDTO ADto(HorarioLocal horario) => new()
        {
            DiaSemana = horario.DiaSemana,
            Modo = horario.Modo,
            Turno1Apertura = Formatear(horario.Turno1Apertura),
            Turno1Cierre = Formatear(horario.Turno1Cierre),
            Turno2Apertura = Formatear(horario.Turno2Apertura),
            Turno2Cierre = Formatear(horario.Turno2Cierre)
        };

        private static TimeOnly? Parsear(string? texto, string campo, string nombreDia, List<string> errores)
        {
            if (TryParsearHora(texto, out var hora))
                return hora;

            errores.Add($"La hora '{texto}' de {campo} del {nombreDia} no es válida: usá el formato HH:mm (por ejemplo 19:30).");
            return null;
        }
    }
}
