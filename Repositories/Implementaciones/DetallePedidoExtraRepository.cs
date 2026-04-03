using Eat_Experience.Data;
using Eat_Experience.Models;
using Eat_Experience.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Eat_Experience.Repositories.Implementaciones
{
    public class DetallePedidoExtraRepository : IDetallePedidoExtraRepository
    {
        private readonly AppDbContext _context;

    public DetallePedidoExtraRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<DetallePedidoExtra>> ObtenerPorDetallePedidoId(int detallePedidoId)
    {
        return await _context.DetallePedidoExtras
            .Where(e => e.DetallePedidoId == detallePedidoId)
            .Include(e => e.ProductoExtra)
            .ToListAsync();
    }

    public async Task Crear(DetallePedidoExtra detallePedidoExtra)
    {
        _context.DetallePedidoExtras.Add(detallePedidoExtra);
        await _context.SaveChangesAsync();
    }

    public async Task EliminarPorDetallePedidoId(int detallePedidoId)
    {
        var extras = _context.DetallePedidoExtras.Where(e => e.DetallePedidoId == detallePedidoId);
        _context.DetallePedidoExtras.RemoveRange(extras);
        await _context.SaveChangesAsync();
    }

}
}
