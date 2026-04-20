using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class DescuentoRepository : IDescuentoRepository
    {
        private readonly AppDbContext _context;

        public DescuentoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Descuento>> ObtenerPorAdminAsync(int adminId, bool? activo)
        {
            var query = _context.Descuentos
                .Include(d => d.Producto)
                .Include(d => d.Categoria)
                .Where(d => d.AdministradorId == adminId);

            if (activo.HasValue)
                query = query.Where(d => d.Activo == activo.Value);

            return await query
                .OrderByDescending(d => d.FechaCreacion)
                .ToListAsync();
        }

        public async Task<Descuento?> ObtenerPorIdAsync(int id, int adminId)
        {
            return await _context.Descuentos
                .Include(d => d.Producto)
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(d => d.Id == id && d.AdministradorId == adminId);
        }

        public async Task<Descuento> CrearAsync(Descuento descuento)
        {
            _context.Descuentos.Add(descuento);
            await _context.SaveChangesAsync();
            return descuento;
        }

        public async Task<Descuento> ActualizarAsync(Descuento descuento)
        {
            _context.Descuentos.Update(descuento);
            await _context.SaveChangesAsync();
            return descuento;
        }
    }
}
