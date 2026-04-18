using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones;

public class StockService : IStockService
{
    private readonly IStockRepository _stockRepository;

    public StockService(IStockRepository stockRepository)
    {
        _stockRepository = stockRepository;
    }

    public async Task DescontarStock(int productoId, int? varianteId, int cantidad, string motivo, int adminId)
    {
        var producto = await _stockRepository.ObtenerProductoConAdmin(productoId, adminId)
            ?? throw new InvalidOperationException("Producto no encontrado.");

        if (varianteId.HasValue)
        {
            var variante = await _stockRepository.ObtenerVariante(varianteId.Value)
                ?? throw new InvalidOperationException("Variante no encontrada.");

            if (variante.Stock == null) return;

            if (variante.Stock - cantidad < 0)
                throw new InvalidOperationException($"Stock insuficiente para '{producto.Nombre}'");

            int stockAnterior = variante.Stock.Value;
            variante.Stock -= cantidad;

            if (variante.Stock == 0 && producto.Administrador.AutoDeshabilitarSinStock)
                variante.Disponible = false;

            await _stockRepository.RegistrarMovimiento(new MovimientoStock
            {
                AdministradorId = adminId,
                ProductoId = productoId,
                VarianteProductoId = varianteId,
                Tipo = "salida",
                Cantidad = cantidad,
                StockAnterior = stockAnterior,
                StockNuevo = variante.Stock.Value,
                Motivo = motivo
            });
        }
        else
        {
            if (producto.Stock == null) return;

            if (producto.Stock - cantidad < 0)
                throw new InvalidOperationException($"Stock insuficiente para '{producto.Nombre}'");

            int stockAnterior = producto.Stock.Value;
            producto.Stock -= cantidad;

            if (producto.Stock == 0 && producto.Administrador.AutoDeshabilitarSinStock)
                producto.Disponible = false;

            await _stockRepository.RegistrarMovimiento(new MovimientoStock
            {
                AdministradorId = adminId,
                ProductoId = productoId,
                Tipo = "salida",
                Cantidad = cantidad,
                StockAnterior = stockAnterior,
                StockNuevo = producto.Stock.Value,
                Motivo = motivo
            });
        }

        await _stockRepository.SaveChanges();
    }

    public async Task ReponerStock(int productoId, int? varianteId, int cantidad, string motivo, int adminId)
    {
        var producto = await _stockRepository.ObtenerProductoConAdmin(productoId, adminId)
            ?? throw new InvalidOperationException("Producto no encontrado.");

        if (varianteId.HasValue)
        {
            var variante = await _stockRepository.ObtenerVariante(varianteId.Value)
                ?? throw new InvalidOperationException("Variante no encontrada.");

            if (variante.Stock == null) return;

            int stockAnterior = variante.Stock.Value;
            variante.Stock += cantidad;

            await _stockRepository.RegistrarMovimiento(new MovimientoStock
            {
                AdministradorId = adminId,
                ProductoId = productoId,
                VarianteProductoId = varianteId,
                Tipo = "entrada",
                Cantidad = cantidad,
                StockAnterior = stockAnterior,
                StockNuevo = variante.Stock.Value,
                Motivo = motivo
            });
        }
        else
        {
            if (producto.Stock == null) return;

            int stockAnterior = producto.Stock.Value;
            producto.Stock += cantidad;

            await _stockRepository.RegistrarMovimiento(new MovimientoStock
            {
                AdministradorId = adminId,
                ProductoId = productoId,
                Tipo = "entrada",
                Cantidad = cantidad,
                StockAnterior = stockAnterior,
                StockNuevo = producto.Stock.Value,
                Motivo = motivo
            });
        }

        await _stockRepository.SaveChanges();
    }

    public async Task AjustarStock(int productoId, int? varianteId, int nuevoStock, string motivo, int adminId)
    {
        var producto = await _stockRepository.ObtenerProductoConAdmin(productoId, adminId)
            ?? throw new InvalidOperationException("Producto no encontrado.");

        if (varianteId.HasValue)
        {
            var variante = await _stockRepository.ObtenerVariante(varianteId.Value)
                ?? throw new InvalidOperationException("Variante no encontrada.");

            if (variante.Stock == null) return;

            int stockAnterior = variante.Stock.Value;
            variante.Stock = nuevoStock;

            if (nuevoStock == 0 && producto.Administrador.AutoDeshabilitarSinStock)
                variante.Disponible = false;

            await _stockRepository.RegistrarMovimiento(new MovimientoStock
            {
                AdministradorId = adminId,
                ProductoId = productoId,
                VarianteProductoId = varianteId,
                Tipo = "ajuste",
                Cantidad = nuevoStock - stockAnterior,
                StockAnterior = stockAnterior,
                StockNuevo = nuevoStock,
                Motivo = motivo
            });
        }
        else
        {
            if (producto.Stock == null) return;

            int stockAnterior = producto.Stock.Value;
            producto.Stock = nuevoStock;

            if (nuevoStock == 0 && producto.Administrador.AutoDeshabilitarSinStock)
                producto.Disponible = false;

            await _stockRepository.RegistrarMovimiento(new MovimientoStock
            {
                AdministradorId = adminId,
                ProductoId = productoId,
                Tipo = "ajuste",
                Cantidad = nuevoStock - stockAnterior,
                StockAnterior = stockAnterior,
                StockNuevo = nuevoStock,
                Motivo = motivo
            });
        }

        await _stockRepository.SaveChanges();
    }
}
