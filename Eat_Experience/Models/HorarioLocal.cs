using System.ComponentModel.DataAnnotations;

namespace Vinto.Api.Models
{
    // Único lugar donde se definen los valores válidos de HorarioLocal.Modo (los usan el CHECK de la
    // tabla, el validador y el cálculo del estado del local). La comparación es ordinal: "24h", no "24H".
    public static class ModoHorario
    {
        public const string Cerrado = "Cerrado";
        public const string VeinticuatroHoras = "24h";
        public const string Turnos = "Turnos";

        public static readonly IReadOnlyList<string> Validos = new[] { Cerrado, VeinticuatroHoras, Turnos };
    }

    // Horario de atención de un local: una fila por día de la semana (único por AdministradorId + DiaSemana).
    // Un local sin ninguna fila se considera siempre abierto.
    public class HorarioLocal
    {
        public int Id { get; set; }

        public int AdministradorId { get; set; }
        public Administrador Administrador { get; set; } = null!;

        /// <summary>0-6, mismo orden que System.DayOfWeek (0 = domingo).</summary>
        public int DiaSemana { get; set; }

        /// <summary>"Cerrado", "24h" o "Turnos".</summary>
        [Required, MaxLength(10)]
        public string Modo { get; set; } = string.Empty;

        // Si el cierre es menor que la apertura, el turno termina al día siguiente (ej. 20:00 a 01:00).
        // Solo se usan con Modo = "Turnos".
        public TimeOnly? Turno1Apertura { get; set; }
        public TimeOnly? Turno1Cierre { get; set; }
        public TimeOnly? Turno2Apertura { get; set; }
        public TimeOnly? Turno2Cierre { get; set; }
    }
}
