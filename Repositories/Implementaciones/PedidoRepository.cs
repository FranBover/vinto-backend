using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class PedidoRepository : IPedidoRepository
    {
        private readonly AppDbContext _context;

        public PedidoRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Pedido>> ObtenerTodos()
        {
            return await _context.Pedidos
                .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
                .ToListAsync();
        }

        public async Task<Pedido?> ObtenerPorId(int id)
        {
            return await _context.Pedidos
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.ProductosExtra)
                        .ThenInclude(e => e.ProductoExtra)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task Crear(Pedido pedido)
        {
            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();
        }



        public async Task Actualizar(Pedido pedido)
        {
            _context.Pedidos.Update(pedido);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var pedido = await _context.Pedidos.FindAsync(id);
            if (pedido != null)
            {
                _context.Pedidos.Remove(pedido);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Pedido>> ObtenerFiltrados(int adminId, string? estado, DateTime? desde, DateTime? hasta, string? formaPago, string? formaEntrega)
        {
            var query = _context.Pedidos
                .Where(p => p.AdministradorId == adminId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(estado))
                query = query.Where(p => p.Estado == estado);

            if (desde.HasValue)
                query = query.Where(p => p.Fecha >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(p => p.Fecha <= hasta.Value);

            if (!string.IsNullOrWhiteSpace(formaPago))
                query = query.Where(p => p.FormaPago == formaPago);

            if (!string.IsNullOrWhiteSpace(formaEntrega))
                query = query.Where(p => p.FormaEntrega == formaEntrega);

            return await query
                .Include(p => p.Detalles)
                .ToListAsync();
        }

        public async Task<IEnumerable<ComentarioPedido>?> GetComentariosAsync(int pedidoId, int adminId)
        {
            var pedidoExiste = await _context.Pedidos
                .AnyAsync(p => p.Id == pedidoId && p.AdministradorId == adminId);

            if (!pedidoExiste)
                return null;

            return await _context.ComentariosPedido
                .Where(c => c.PedidoId == pedidoId)
                .OrderBy(c => c.FechaCreacion)
                .ToListAsync();
        }

        public async Task AddComentarioAsync(ComentarioPedido comentario)
        {
            _context.ComentariosPedido.Add(comentario);
            await _context.SaveChangesAsync();
        }

        public async Task<Pedido?> GetComandaAsync(int pedidoId, int adminId)
            => await GetPedidoConTodo(pedidoId, adminId);

        public async Task<Pedido?> GetTicketAsync(int pedidoId, int adminId)
            => await GetPedidoConTodo(pedidoId, adminId);

        private async Task<Pedido?> GetPedidoConTodo(int pedidoId, int adminId)
        {
            return await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Administrador)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.ProductosExtra)
                        .ThenInclude(e => e.ProductoExtra)
                .FirstOrDefaultAsync(p => p.Id == pedidoId && p.AdministradorId == adminId);
        }

    }
}
