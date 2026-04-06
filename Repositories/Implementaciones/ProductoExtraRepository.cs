using Eat_Experience.Data;
using Eat_Experience.Models;
using Eat_Experience.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Eat_Experience.Repositories.Implementaciones
{
    public class ProductoExtraRepository : IProductoExtraRepository
    {
        private readonly AppDbContext _context;

        public ProductoExtraRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProductoExtra>> ObtenerTodos()
        {
            return await _context.ProductoExtras.ToListAsync();
        }

        public async Task<IEnumerable<ProductoExtra>> ObtenerPorAdministradorId(int adminId)
        {
            return await _context.ProductoExtras
                .Include(e => e.Producto)
                .Where(e => e.Producto.AdministradorId == adminId)
                .ToListAsync();
        }

        public async Task<ProductoExtra?> ObtenerPorId(int id)
        {
            return await _context.ProductoExtras
                .Include(e => e.Producto)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<IEnumerable<ProductoExtra>> ObtenerPorProductoId(int productoId)
        {
            return await _context.ProductoExtras
                .Where(e => e.ProductoId == productoId)
                .ToListAsync();
        }

        public async Task Crear(ProductoExtra extra)
        {
            _context.ProductoExtras.Add(extra);
            await _context.SaveChangesAsync();
        }

        public async Task Actualizar(ProductoExtra extra)
        {
            _context.ProductoExtras.Update(extra);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var extra = await ObtenerPorId(id);
            if (extra != null)
            {
                _context.ProductoExtras.Remove(extra);
                await _context.SaveChangesAsync();
            }
        }
    }
}
