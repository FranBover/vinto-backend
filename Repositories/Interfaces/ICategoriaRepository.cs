using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface ICategoriaRepository
    {
        Task<IEnumerable<Categoria>> ObtenerTodas();
        Task<IEnumerable<Categoria>> ObtenerPorAdministradorId(int adminId);
        Task<Categoria?> ObtenerPorId(int id);
        Task Crear(Categoria categoria);
        Task Actualizar(Categoria categoria);
        Task Eliminar(int id);
    }
}
