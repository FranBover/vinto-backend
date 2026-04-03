using Eat_Experience.Models;

namespace Eat_Experience.Services.Interfaces
{
    public interface ICategoriaService
    {
        Task<IEnumerable<Categoria>> ObtenerTodas();
        Task<Categoria?> ObtenerPorId(int id);
        Task Crear(Categoria categoria);
        Task Actualizar(Categoria categoria);
        Task Eliminar(int id);
    }
}
