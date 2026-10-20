using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    /// <summary>
    /// Estado de atención de un local para los flujos públicos que parten de un <see cref="Administrador"/> ya
    /// resuelto por slug (pedidos). El "ahora" sale de <see cref="TimeProvider"/>. Falla abierto: un horario que no se
    /// puede evaluar se loguea como error y el local se trata como abierto, igual que en el menú.
    /// </summary>
    public interface IEstadoLocalService
    {
        Task<EstadoLocal> ObtenerAsync(Administrador admin);

        /// <summary>
        /// Lanza <see cref="Helpers.LocalCerradoException"/> (409) con un mensaje útil si el local está cerrado.
        /// Va al principio de lo que INICIA un pedido, antes de crear nada.
        /// </summary>
        Task AsegurarAbiertoAsync(Administrador admin);
    }
}
