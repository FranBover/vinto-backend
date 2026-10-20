using System.Globalization;
using Vinto.Api.Models;

namespace Eat_Experience.Tests;

// Arma horarios de prueba sin repetir el armado de entidades en cada test.
internal static class Horarios
{
    public const string Argentina = "America/Argentina/Buenos_Aires";

    // Mismo orden que System.DayOfWeek.
    public const int Domingo = 0, Lunes = 1, Martes = 2, Miercoles = 3, Jueves = 4, Viernes = 5, Sabado = 6;

    public static TimeOnly Hora(string valor) => TimeOnly.Parse(valor, CultureInfo.InvariantCulture);

    public static HorarioLocal Cerrado(int dia) => new() { DiaSemana = dia, Modo = ModoHorario.Cerrado };

    public static HorarioLocal Dia24h(int dia) => new() { DiaSemana = dia, Modo = ModoHorario.VeinticuatroHoras };

    public static HorarioLocal Turnos(int dia, string apertura1, string cierre1, string? apertura2 = null, string? cierre2 = null) => new()
    {
        DiaSemana = dia,
        Modo = ModoHorario.Turnos,
        Turno1Apertura = Hora(apertura1),
        Turno1Cierre = Hora(cierre1),
        Turno2Apertura = apertura2 == null ? null : Hora(apertura2),
        Turno2Cierre = cierre2 == null ? null : Hora(cierre2)
    };

    // Semana completa: los días que no se pasan quedan en "Cerrado".
    public static List<HorarioLocal> Semana(params HorarioLocal[] dias)
    {
        var porDia = dias.ToDictionary(d => d.DiaSemana);
        return Enumerable.Range(0, 7)
            .Select(dia => porDia.TryGetValue(dia, out var horario) ? horario : Cerrado(dia))
            .ToList();
    }

    public static List<HorarioLocal> Semana24h()
        => Enumerable.Range(0, 7).Select(Dia24h).ToList();

    // Hora de pared argentina (UTC-3, sin horario de verano) expresada como instante UTC.
    public static DateTimeOffset EnArgentina(int anio, int mes, int dia, int hora, int minuto)
        => new DateTimeOffset(anio, mes, dia, hora, minuto, 0, TimeSpan.FromHours(-3)).ToUniversalTime();
}
