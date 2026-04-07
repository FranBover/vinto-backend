using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IAdministradorService
    {
        Task<IEnumerable<Administrador>> ObtenerTodos();
        Task<Administrador?> ObtenerPorId(int id);
        Task Crear(Administrador administrador);
        Task Actualizar(Administrador administrador);
        Task Eliminar(int id);
    }
}
