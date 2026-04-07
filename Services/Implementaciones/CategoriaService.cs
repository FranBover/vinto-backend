using Vinto.Api.Models;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class CategoriaService : ICategoriaService
    {
        private readonly ICategoriaRepository _categoriaRepository;

        public CategoriaService(ICategoriaRepository categoriaRepository)
        {
            _categoriaRepository = categoriaRepository;
        }

        public async Task<IEnumerable<Categoria>> ObtenerTodas()
        {
            return await _categoriaRepository.ObtenerTodas();
        }

        public async Task<IEnumerable<Categoria>> ObtenerPorAdministradorId(int adminId)
        {
            return await _categoriaRepository.ObtenerPorAdministradorId(adminId);
        }

        public async Task<Categoria?> ObtenerPorId(int id)
        {
            return await _categoriaRepository.ObtenerPorId(id);
        }

        public async Task Crear(Categoria categoria)
        {
            await _categoriaRepository.Crear(categoria);
        }

        public async Task Actualizar(Categoria categoria)
        {
            await _categoriaRepository.Actualizar(categoria);
        }

        public async Task Eliminar(int id)
        {
            await _categoriaRepository.Eliminar(id);
        }
    }
}
