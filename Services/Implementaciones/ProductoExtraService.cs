using Eat_Experience.Models;
using Eat_Experience.Repositories.Interfaces;
using Eat_Experience.Services.Interfaces;

namespace Eat_Experience.Services.Implementaciones
{
    public class ProductoExtraService : IProductoExtraService
    {
        private readonly IProductoExtraRepository _productoExtraRepository;

        public ProductoExtraService(IProductoExtraRepository productoExtraRepository)
        {
            _productoExtraRepository = productoExtraRepository;
        }

        public async Task<IEnumerable<ProductoExtra>> ObtenerTodos()
        {
            return await _productoExtraRepository.ObtenerTodos();
        }

        public async Task<IEnumerable<ProductoExtra>> ObtenerPorAdministradorId(int adminId)
        {
            return await _productoExtraRepository.ObtenerPorAdministradorId(adminId);
        }

        public async Task<ProductoExtra?> ObtenerPorId(int id)
        {
            return await _productoExtraRepository.ObtenerPorId(id);
        }

        public async Task Crear(ProductoExtra extra)
        {
            await _productoExtraRepository.Crear(extra);
        }

        public async Task Actualizar(ProductoExtra extra)
        {
            await _productoExtraRepository.Actualizar(extra);
        }

        public async Task Eliminar(int id)
        {
            await _productoExtraRepository.Eliminar(id);
        }

        public async Task<IEnumerable<ProductoExtra>> ObtenerPorProductoId(int productoId)
        {
            return await _productoExtraRepository.ObtenerPorProductoId(productoId);
        }
    }
}
