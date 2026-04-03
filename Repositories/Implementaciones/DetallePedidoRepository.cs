using Eat_Experience.Data;
using Eat_Experience.Models;
using Eat_Experience.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Eat_Experience.Repositories.Implementaciones
{
    public class DetallePedidoRepository : IDetallePedidoRepository
    {
        private readonly AppDbContext _context;

        public DetallePedidoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DetallePedido>> ObtenerTodos()
        {
            return await _context.DetallesPedido
                .Include(d => d.Producto)
                .Include(d => d.Pedido)
                .ToListAsync();
        }

        public async Task<DetallePedido?> ObtenerPorId(int id)
        {
            return await _context.DetallesPedido
                .Include(d => d.Producto)
                .Include(d => d.Pedido)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task Crear(DetallePedido detallePedido)
        {
            _context.DetallesPedido.Add(detallePedido);
            await _context.SaveChangesAsync();
        }

        public async Task Actualizar(DetallePedido detallePedido)
        {
            _context.DetallesPedido.Update(detallePedido);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var detalle = await _context.DetallesPedido.FindAsync(id);
            if (detalle != null)
            {
                _context.DetallesPedido.Remove(detalle);
                await _context.SaveChangesAsync();
            }
        }
    }
}
