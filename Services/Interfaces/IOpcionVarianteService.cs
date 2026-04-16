using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IOpcionVarianteService
    {
        Task<IEnumerable<OpcionVarianteResponseDTO>> ObtenerPorTipoVarianteId(int tipoVarianteId);
        Task<OpcionVariante?> ObtenerPorId(int id);
        Task<bool> ExisteValorEnTipo(int tipoVarianteId, string valor, int? excludeId = null);
        Task<bool> TieneVariantesAsociadas(int opcionId);
        Task Crear(OpcionVariante opcion);
        Task Actualizar(OpcionVariante opcion);
        Task Eliminar(int id);
    }
}
