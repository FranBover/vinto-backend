using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IVarianteProductoService
    {
        Task<IEnumerable<VarianteProductoResponseDTO>> ObtenerPorProductoId(int productoId);
        Task<VarianteProducto?> ObtenerPorId(int id);
        Task<(string? error, IEnumerable<VarianteProductoResponseDTO>? variantes)> Generar(int productoId);
        Task Actualizar(VarianteProducto variante);
        Task Eliminar(int id);
    }
}
