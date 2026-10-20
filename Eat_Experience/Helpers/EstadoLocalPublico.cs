using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Helpers
{
    // Cálculo del estado del local para lo que ve el público (menú, pedidos). A diferencia del panel, acá un horario
    // roto NO puede tumbar la tienda: si el cálculo falla (típicamente una zona horaria inválida guardada en la base)
    // se loguea un error y el local se trata como abierto ("fallar abierto"), igual que uno sin horarios.
    public static class EstadoLocalPublico
    {
        public static EstadoLocal Calcular(
            IEstadoLocalCalculator calculator,
            IReadOnlyList<HorarioLocal> horarios,
            bool cerradoManualmente,
            string zonaHoraria,
            DateTimeOffset ahoraUtc,
            ILogger logger,
            int administradorId)
        {
            try
            {
                return calculator.Calcular(horarios, cerradoManualmente, zonaHoraria, ahoraUtc);
            }
            catch (Exception ex)
            {
                // Cualquier falla, no solo ArgumentException: la tienda pública no se cae por los horarios.
                // Queda a la vista en el log como Error (no se traga en silencio).
                logger.LogError(ex,
                    "No se pudo calcular el estado del local {AdministradorId} (zona horaria '{ZonaHoraria}'); se lo trata como abierto.",
                    administradorId, zonaHoraria);
                return new EstadoLocal(true, MotivoEstadoLocal.SinHorarios, null);
            }
        }
    }
}
