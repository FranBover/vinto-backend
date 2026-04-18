using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;

namespace Vinto.Api.Repositories.Implementaciones;

public class StockRepository : IStockRepository
{
    private readonly AppDbContext _context;

    public StockRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Producto?> ObtenerProductoConAdmin(int productoId, int adminId)
    {
        return await _context.Productos
            .Include(p => p.Administrador)
            .FirstOrDefaultAsync(p => p.Id == productoId && p.AdministradorId == adminId);
    }

    public async Task<VarianteProducto?> ObtenerVariante(int varianteId)
    {
        return await _context.VariantesProducto
            .FirstOrDefaultAsync(v => v.Id == varianteId);
    }

    public Task RegistrarMovimiento(MovimientoStock movimiento)
    {
        _context.MovimientosStock.Add(movimiento);
        return Task.CompletedTask;
    }

    public async Task SaveChanges()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<Producto?> ObtenerProductoParaStock(int productoId, int adminId)
    {
        return await _context.Productos
            .Include(p => p.Variantes)
                .ThenInclude(v => v.Opcion1)
            .Include(p => p.Variantes)
                .ThenInclude(v => v.Opcion2)
            .FirstOrDefaultAsync(p => p.Id == productoId && p.AdministradorId == adminId);
    }

    public async Task<List<MovimientoStock>> ObtenerUltimosMovimientos(int productoId, int adminId, int cantidad)
    {
        return await _context.MovimientosStock
            .Where(m => m.ProductoId == productoId && m.AdministradorId == adminId)
            .OrderByDescending(m => m.FechaCreacion)
            .Take(cantidad)
            .ToListAsync();
    }

    public async Task<int> ObtenerUmbralAlerta(int adminId)
    {
        var umbral = await _context.Administradores
            .Where(a => a.Id == adminId)
            .Select(a => a.StockBajoAlerta)
            .FirstOrDefaultAsync();

        return umbral ?? 5;
    }

    public async Task<List<Producto>> ObtenerProductosStockBajo(int adminId, int umbral)
    {
        return await _context.Productos
            .Where(p => p.AdministradorId == adminId
                && !p.TieneVariantes
                && p.Stock != null
                && p.Stock <= umbral)
            .ToListAsync();
    }

    public async Task<List<VarianteProducto>> ObtenerVariantesStockBajo(int adminId, int umbral)
    {
        return await _context.VariantesProducto
            .Include(v => v.Producto)
            .Include(v => v.Opcion1)
            .Include(v => v.Opcion2)
            .Where(v => v.Producto.AdministradorId == adminId
                && v.Stock != null
                && v.Stock <= umbral)
            .ToListAsync();
    }
}
