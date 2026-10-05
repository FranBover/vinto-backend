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

        public async Task<(List<PedidoListItemResponseDTO> Items, int Total)> ObtenerFiltradosPaginado(
            int adminId, string? estado, DateTime? desde, DateTime? hasta, string? formaPago, string? formaEntrega,
            int page, int pageSize)
        {
            var query = _context.Pedidos
                .AsNoTracking()
                .Where(p => p.AdministradorId == adminId);

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

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(p => p.Fecha)
                .ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PedidoListItemResponseDTO
                {
                    Id = p.Id,
                    Fecha = p.Fecha,
                    Estado = p.Estado,
                    NombreCliente = p.NombreCliente,
                    FormaPago = p.FormaPago,
                    FormaEntrega = p.FormaEntrega,
                    Total = p.Total,
                    ItemsCount = p.Detalles.Count
                })
                .ToListAsync();

            return (items, total);
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
