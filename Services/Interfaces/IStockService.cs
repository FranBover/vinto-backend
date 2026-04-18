namespace Vinto.Api.Services.Interfaces;

public interface IStockService
{
    Task DescontarStock(int productoId, int? varianteId, int cantidad, string motivo, int adminId);
    Task ReponerStock(int productoId, int? varianteId, int cantidad, string motivo, int adminId);
    Task AjustarStock(int productoId, int? varianteId, int nuevoStock, string motivo, int adminId);
}
