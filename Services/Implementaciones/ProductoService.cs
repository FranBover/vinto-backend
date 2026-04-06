using Eat_Experience.Models;
using Eat_Experience.Repositories.Interfaces;
using Eat_Experience.Services.Interfaces;

namespace Eat_Experience.Services.Implementaciones
{
    public class ProductoService : IProductoService
    {
        private readonly IProductoRepository _productoRepository;

        public ProductoService(IProductoRepository productoRepository)
        {
            _productoRepository = productoRepository;
        }

        public async Task<IEnumerable<Producto>> ObtenerTodos()
        {
            return await _productoRepository.ObtenerTodos();
        }

        public async Task<IEnumerable<Producto>> ObtenerPorAdministradorId(int adminId)
        {
            return await _productoRepository.ObtenerPorAdministradorId(adminId);
        }

        public async Task<Producto?> ObtenerPorId(int id)
        {
            return await _productoRepository.ObtenerPorId(id);
        }

        public async Task Crear(Producto producto)
        {
            await _productoRepository.Crear(producto);
        }

        public async Task Actualizar(Producto producto)
        {
            await _productoRepository.Actualizar(producto);
        }

        public async Task Eliminar(int id)
        {
            await _productoRepository.Eliminar(id);
        }
    }
}
