using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface IOpcionVarianteRepository
    {
        Task<IEnumerable<OpcionVariante>> ObtenerPorTipoVarianteId(int tipoVarianteId);
        Task<OpcionVariante?> ObtenerPorId(int id);
        Task<bool> ExisteValorEnTipo(int tipoVarianteId, string valor, int? excludeId = null);
        Task<bool> TieneVariantesAsociadas(int opcionId);
        Task Crear(OpcionVariante opcion);
        Task Actualizar(OpcionVariante opcion);
        Task Eliminar(int id);
    }
}
