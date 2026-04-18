using Vinto.Api.Models;

namespace Vinto.Api.Repositories.Interfaces;

public interface IStockRepository
{
    Task<Producto?> ObtenerProductoConAdmin(int productoId, int adminId);
    Task<VarianteProducto?> ObtenerVariante(int varianteId);
    Task RegistrarMovimiento(MovimientoStock movimiento);
    Task SaveChanges();

    Task<Producto?> ObtenerProductoParaStock(int productoId, int adminId);
    Task<List<MovimientoStock>> ObtenerUltimosMovimientos(int productoId, int adminId, int cantidad);
    Task<int> ObtenerUmbralAlerta(int adminId);
    Task<List<Producto>> ObtenerProductosStockBajo(int adminId, int umbral);
    Task<List<VarianteProducto>> ObtenerVariantesStockBajo(int adminId, int umbral);
}
