using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class VarianteProductoService : IVarianteProductoService
    {
        private readonly IVarianteProductoRepository _varianteRepo;
        private readonly IProductoRepository _productoRepo;
        private readonly ITipoVarianteRepository _tipoVarianteRepo;

        public VarianteProductoService(
            IVarianteProductoRepository varianteRepo,
            IProductoRepository productoRepo,
            ITipoVarianteRepository tipoVarianteRepo)
        {
            _varianteRepo = varianteRepo;
            _productoRepo = productoRepo;
            _tipoVarianteRepo = tipoVarianteRepo;
        }

        public async Task<IEnumerable<VarianteProductoResponseDTO>> ObtenerPorProductoId(int productoId)
        {
            var variantes = await _varianteRepo.ObtenerPorProductoId(productoId);
            return variantes.Select(MapToDto);
        }

        public async Task<VarianteProducto?> ObtenerPorId(int id)
        {
            return await _varianteRepo.ObtenerPorId(id);
        }

        public async Task<(string? error, IEnumerable<VarianteProductoResponseDTO>? variantes)> Generar(int productoId)
        {
            var producto = await _productoRepo.ObtenerPorId(productoId);
            if (producto == null)
                return ("Producto no encontrado.", null);

            var tipos = (await _tipoVarianteRepo.ObtenerPorProductoId(productoId)).ToList();
            if (tipos.Count == 0)
                return ("El producto no tiene tipos de variante definidos.", null);

            // Construir el set de combinaciones ya existentes para filtrar duplicados
            var existentes = (await _varianteRepo.ObtenerPorProductoId(productoId)).ToList();
            var existentesSet = new HashSet<(int, int?)>(
                existentes.Select(v => (v.Opcion1Id, v.Opcion2Id))
            );

            var nuevas = new List<VarianteProducto>();

            if (tipos.Count == 1)
            {
                // Una variante por cada opción del único tipo
                foreach (var opcion in tipos[0].Opciones.OrderBy(o => o.Orden))
                {
                    if (!existentesSet.Contains((opcion.Id, null)))
                    {
                        nuevas.Add(new VarianteProducto
                        {
                            ProductoId = productoId,
                            Opcion1Id = opcion.Id,
                            Opcion2Id = null,
                            Precio = producto.Precio,
                            Disponible = true
                        });
                    }
                }
            }
            else
            {
                // Producto cartesiano: tipo[0] × tipo[1]
                var tipo1 = tipos.OrderBy(t => t.Orden).First();
                var tipo2 = tipos.OrderBy(t => t.Orden).Last();

                foreach (var op1 in tipo1.Opciones.OrderBy(o => o.Orden))
                {
                    foreach (var op2 in tipo2.Opciones.OrderBy(o => o.Orden))
                    {
                        if (!existentesSet.Contains((op1.Id, (int?)op2.Id)))
                        {
                            nuevas.Add(new VarianteProducto
                            {
                                ProductoId = productoId,
                                Opcion1Id = op1.Id,
                                Opcion2Id = op2.Id,
                                Precio = producto.Precio,
                                Disponible = true
                            });
                        }
                    }
                }
            }

            if (nuevas.Count > 0)
                await _varianteRepo.CrearRango(nuevas);

            // Marcar el producto con TieneVariantes = true si no lo estaba
            if (!producto.TieneVariantes)
            {
                producto.TieneVariantes = true;
                await _productoRepo.Actualizar(producto);
            }

            var todas = await ObtenerPorProductoId(productoId);
            return (null, todas);
        }

        public async Task Actualizar(VarianteProducto variante)
        {
            await _varianteRepo.Actualizar(variante);
        }

        public async Task Eliminar(int id)
        {
            await _varianteRepo.Eliminar(id);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static VarianteProductoResponseDTO MapToDto(VarianteProducto v)
        {
            var opcion1Dto = new OpcionVarianteResponseDTO
            {
                Id = v.Opcion1.Id,
                Valor = v.Opcion1.Valor,
                Orden = v.Opcion1.Orden,
                TipoVarianteId = v.Opcion1.TipoVarianteId
            };

            OpcionVarianteResponseDTO? opcion2Dto = null;
            if (v.Opcion2 != null)
            {
                opcion2Dto = new OpcionVarianteResponseDTO
                {
                    Id = v.Opcion2.Id,
                    Valor = v.Opcion2.Valor,
                    Orden = v.Opcion2.Orden,
                    TipoVarianteId = v.Opcion2.TipoVarianteId
                };
            }

            var descripcion = opcion2Dto != null
                ? $"{opcion1Dto.Valor} / {opcion2Dto.Valor}"
                : opcion1Dto.Valor;

            return new VarianteProductoResponseDTO
            {
                Id = v.Id,
                ProductoId = v.ProductoId,
                Opcion1 = opcion1Dto,
                Opcion2 = opcion2Dto,
                Precio = v.Precio,
                Stock = v.Stock,
                Disponible = v.Disponible,
                Sku = v.Sku,
                Descripcion = descripcion
            };
        }
    }
}
