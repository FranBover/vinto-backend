using Vinto.Api.DTOs;

namespace Vinto.Api.Services.Interfaces
{
    /// <summary>
    /// Horarios de atención y cierre manual del local. Todo se resuelve por <c>adminId</c> (el del JWT);
    /// los métodos devuelven null si ese administrador no existe.
    /// </summary>
    public interface IHorarioLocalService
    {
        Task<HorariosLocalResponseDTO?> ObtenerAsync(int adminId);

        /// <summary>
        /// Reemplaza zona horaria y horarios en un único SaveChanges (una transacción): o queda todo o no cambia nada.
        /// Lanza <see cref="Helpers.HorariosInvalidosException"/> con todos los errores si algo no es válido.
        /// </summary>
        Task<HorariosLocalResponseDTO?> ReemplazarAsync(int adminId, HorariosLocalUpdateDTO dto);

        /// <summary>Fija el cierre manual y devuelve el estado resultante.</summary>
        Task<EstadoLocal?> FijarCierreManualAsync(int adminId, bool cerrado);
    }
}
