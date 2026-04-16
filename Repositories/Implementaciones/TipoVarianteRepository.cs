using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class TipoVarianteRepository : ITipoVarianteRepository
    {
        private readonly AppDbContext _context;

        public TipoVarianteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<TipoVariante>> ObtenerPorProductoId(int productoId)
        {
            return await _context.TiposVariante
                .Include(t => t.Opciones)
                .Where(t => t.ProductoId == productoId)
                .OrderBy(t => t.Orden)
                .ToListAsync();
        }

        public async Task<TipoVariante?> ObtenerPorId(int id)
        {
            return await _context.TiposVariante.FindAsync(id);
        }

        public async Task<int> ContarPorProductoId(int productoId)
        {
            return await _context.TiposVariante
                .CountAsync(t => t.ProductoId == productoId);
        }

        public async Task Crear(TipoVariante tipo)
        {
            _context.TiposVariante.Add(tipo);
            await _context.SaveChangesAsync();
        }

        public async Task Actualizar(TipoVariante tipo)
        {
            _context.TiposVariante.Update(tipo);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var tipo = await _context.TiposVariante.FindAsync(id);
            if (tipo != null)
            {
                _context.TiposVariante.Remove(tipo);
                await _context.SaveChangesAsync();
            }
        }
    }
}
