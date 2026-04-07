using Vinto.Api.Models;
using Vinto.Api.Data;
using Microsoft.EntityFrameworkCore;
using Vinto.Api.Repositories.Interfaces;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class ProductoRepository : IProductoRepository
    {
        private readonly AppDbContext _context;

        public ProductoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Producto>> ObtenerTodos()
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Extras)
                .ToListAsync();
        }

        public async Task<IEnumerable<Producto>> ObtenerPorAdministradorId(int adminId)
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Extras)
                .Where(p => p.AdministradorId == adminId)
                .ToListAsync();
        }

        public async Task<Producto?> ObtenerPorId(int id)
        {
            return await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Extras)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task Crear(Producto producto)
        {
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();
        }

        public async Task Actualizar(Producto producto)
        {
            _context.Productos.Update(producto);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
            }
        }
    }
}
