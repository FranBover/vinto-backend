using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class VarianteProductoRepository : IVarianteProductoRepository
    {
        private readonly AppDbContext _context;

        public VarianteProductoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<VarianteProducto>> ObtenerPorProductoId(int productoId)
        {
            return await _context.VariantesProducto
                .Include(v => v.Opcion1)
                .Include(v => v.Opcion2)
                .Where(v => v.ProductoId == productoId)
                .OrderBy(v => v.Opcion1.Orden)
                .ThenBy(v => v.Opcion2 != null ? v.Opcion2.Orden : 0)
                .ToListAsync();
        }

        public async Task<VarianteProducto?> ObtenerPorId(int id)
        {
            return await _context.VariantesProducto.FindAsync(id);
        }

        public async Task CrearRango(IEnumerable<VarianteProducto> variantes)
        {
            _context.VariantesProducto.AddRange(variantes);
            await _context.SaveChangesAsync();
        }

        public async Task Actualizar(VarianteProducto variante)
        {
            _context.VariantesProducto.Update(variante);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var variante = await _context.VariantesProducto.FindAsync(id);
            if (variante != null)
            {
                _context.VariantesProducto.Remove(variante);
                await _context.SaveChangesAsync();
            }
        }

        public async Task EliminarTodas(int productoId)
        {
            await _context.VariantesProducto
                .Where(v => v.ProductoId == productoId)
                .ExecuteDeleteAsync();
        }
    }
}
