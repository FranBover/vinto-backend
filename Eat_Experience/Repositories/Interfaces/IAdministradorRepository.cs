using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface IAdministradorRepository
    {
        Task<IEnumerable<Administrador>> ObtenerTodos();
        Task<Administrador?> ObtenerPorId(int id);
        // Única resolución de local por slug público: solo locales activos, filtrado en SQL.
        Task<Administrador?> ObtenerActivoPorSlugAsync(string slug);
        Task<bool> ExisteSlugAsync(string slug, int? excluirAdminId = null);
        Task<string> GenerarSlugUnicoAsync(string baseSlug);
        Task Crear(Administrador administrador);
        Task Actualizar(Administrador administrador);
        Task Eliminar(int id);
    }
}
