using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class TipoVarianteService : ITipoVarianteService
    {
        private readonly ITipoVarianteRepository _tipoVarianteRepository;
        private readonly IProductoRepository _productoRepository;

        public TipoVarianteService(
            ITipoVarianteRepository tipoVarianteRepository,
            IProductoRepository productoRepository)
        {
            _tipoVarianteRepository = tipoVarianteRepository;
            _productoRepository = productoRepository;
        }

        public async Task<IEnumerable<TipoVarianteResponseDTO>> ObtenerPorProductoId(int productoId)
        {
            var tipos = await _tipoVarianteRepository.ObtenerPorProductoId(productoId);
            return tipos.Select(t => new TipoVarianteResponseDTO
            {
                Id = t.Id,
                Nombre = t.Nombre,
                Orden = t.Orden,
                CantidadOpciones = t.Opciones.Count
            });
        }

        public async Task<TipoVariante?> ObtenerPorId(int id)
        {
            return await _tipoVarianteRepository.ObtenerPorId(id);
        }

        public async Task<int> ContarPorProductoId(int productoId)
        {
            return await _tipoVarianteRepository.ContarPorProductoId(productoId);
        }

        public async Task Crear(TipoVariante tipo)
        {
            await _tipoVarianteRepository.Crear(tipo);

            // Asegurar que el producto quede marcado con TieneVariantes = true
            var producto = await _productoRepository.ObtenerPorId(tipo.ProductoId);
            if (producto != null && !producto.TieneVariantes)
            {
                producto.TieneVariantes = true;
                await _productoRepository.Actualizar(producto);
            }
        }

        public async Task Actualizar(TipoVariante tipo)
        {
            await _tipoVarianteRepository.Actualizar(tipo);
        }

        public async Task Eliminar(int id, int productoId)
        {
            await _tipoVarianteRepository.Eliminar(id);

            // Si ya no quedan tipos de variante, desactivar la bandera en el producto
            var restantes = await _tipoVarianteRepository.ContarPorProductoId(productoId);
            if (restantes == 0)
            {
                var producto = await _productoRepository.ObtenerPorId(productoId);
                if (producto != null && producto.TieneVariantes)
                {
                    producto.TieneVariantes = false;
                    await _productoRepository.Actualizar(producto);
                }
            }
        }
    }
}
