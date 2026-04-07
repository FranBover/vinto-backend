using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces
{
    public interface IAdministradorRepository
    {
        Task<IEnumerable<Administrador>> ObtenerTodos();
        Task<Administrador?> ObtenerPorId(int id);
        Task Crear(Administrador administrador);
        Task Actualizar(Administrador administrador);
        Task Eliminar(int id);
    }
}
