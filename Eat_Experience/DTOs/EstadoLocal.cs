namespace Vinto.Api.DTOs
{
    // Por qué un local está abierto o cerrado en un instante dado.
    public static class MotivoEstadoLocal
    {
        public const string SinHorarios = "SinHorarios";
        public const string Horario = "Horario";
        public const string CerradoManualmente = "CerradoManualmente";
    }

    /// <summary>
    /// Estado de atención de un local. <paramref name="ProximoCambio"/> (UTC) es el próximo cierre si está abierto
    /// o la próxima apertura si está cerrado; null si no hay un cambio previsible.
    /// </summary>
    public sealed record EstadoLocal(bool EstaAbierto, string Motivo, DateTimeOffset? ProximoCambio);
}
