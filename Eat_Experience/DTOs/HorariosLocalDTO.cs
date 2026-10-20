namespace Vinto.Api.DTOs
{
    /// <summary>
    /// Un día de la semana. <c>DiaSemana</c> sigue el orden de System.DayOfWeek (0 = domingo ... 6 = sábado).
    /// Las horas viajan como "HH:mm"; un cierre menor que la apertura termina al día siguiente ("00:00" = medianoche).
    /// Los turnos solo se usan con Modo = "Turnos".
    /// </summary>
    public class HorarioDiaDTO
    {
        public int DiaSemana { get; set; }
        public string Modo { get; set; } = string.Empty;
        public string? Turno1Apertura { get; set; }
        public string? Turno1Cierre { get; set; }
        public string? Turno2Apertura { get; set; }
        public string? Turno2Cierre { get; set; }
    }

    // Reemplaza todos los horarios del local. Dias: 7 elementos, o vacío para "sin horarios fijos" (siempre abierto).
    // No lleva AdministradorId: el local sale del JWT.
    public class HorariosLocalUpdateDTO
    {
        public string? ZonaHoraria { get; set; }
        public List<HorarioDiaDTO>? Dias { get; set; }
    }

    public class HorariosLocalResponseDTO
    {
        public string ZonaHoraria { get; set; } = string.Empty;
        public bool CerradoManualmente { get; set; }
        public List<HorarioDiaDTO> Dias { get; set; } = new();
        public EstadoLocal Estado { get; set; } = null!;
    }

    // Nullable a propósito: si falta "cerrado" en el body no se debe interpretar como false (reabrir el local).
    public class CierreManualUpdateDTO
    {
        public bool? Cerrado { get; set; }
    }
}
