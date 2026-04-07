using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface ICategoriaService
    {
        Task<IEnumerable<Categoria>> ObtenerTodas();
        Task<IEnumerable<Categoria>> ObtenerPorAdministradorId(int adminId);
        Task<Categoria?> ObtenerPorId(int id);
        Task Crear(Categoria categoria);
        Task Actualizar(Categoria categoria);
        Task Eliminar(int id);
    }
}
