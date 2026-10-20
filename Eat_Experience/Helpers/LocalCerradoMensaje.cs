using System.Globalization;
using Vinto.Api.DTOs;

namespace Vinto.Api.Helpers
{
    // Texto que ve el cliente cuando intenta pedir con el local cerrado. Pura: el instante y la zona entran
    // por parámetro. La hora de reapertura se dice en la zona del LOCAL (no en UTC ni en la del servidor).
    public static class LocalCerradoMensaje
    {
        private const string SinHora = "El local está cerrado por el momento.";

        public static string Armar(EstadoLocal estado, string zonaHoraria, DateTimeOffset ahoraUtc)
        {
            // Cierre manual: nadie sabe cuándo se vuelve a abrir. Sin próxima apertura (todo cerrado): igual.
            if (estado.Motivo == MotivoEstadoLocal.CerradoManualmente || estado.ProximoCambio is not { } apertura)
                return SinHora;

            // Un dato de zona roto no debe convertir el rechazo en un error: se informa sin hora.
            if (string.IsNullOrWhiteSpace(zonaHoraria) || !TimeZoneInfo.TryFindSystemTimeZoneById(zonaHoraria, out var zona))
                return SinHora;

            var aperturaLocal = TimeZoneInfo.ConvertTime(apertura, zona).DateTime;
            var ahoraLocal = TimeZoneInfo.ConvertTime(ahoraUtc, zona).DateTime;
            var dias = DateOnly.FromDateTime(aperturaLocal).DayNumber - DateOnly.FromDateTime(ahoraLocal).DayNumber;

            var cuando = dias switch
            {
                <= 0 => "hoy",
                1 => "mañana",
                < 7 => $"el {HorarioLocalValidator.NombreDelDia((int)aperturaLocal.DayOfWeek)}",
                _ => $"el próximo {HorarioLocalValidator.NombreDelDia((int)aperturaLocal.DayOfWeek)}"
            };

            // "a la 01:00" / "a las 19:00"
            var articulo = aperturaLocal.Hour == 1 ? "a la" : "a las";
            var hora = aperturaLocal.ToString("HH:mm", CultureInfo.InvariantCulture);

            return $"El local está cerrado. Abre {cuando} {articulo} {hora}.";
        }
    }
}
