using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vinto.Api.Controllers
{
    [Route("api")]
    [ApiController]
    [Authorize]
    public class StockController : ControllerBase
    {
        private readonly IStockService _stockService;
        private readonly IStockRepository _stockRepository;

        public StockController(IStockService stockService, IStockRepository stockRepository)
        {
            _stockService = stockService;
            _stockRepository = stockRepository;
        }

        [HttpGet("Productos/{productoId}/stock")]
        public async Task<IActionResult> ObtenerStock(int productoId)
        {
            var adminId = ObtenerAdminId();
            if (adminId == null) return Unauthorized();

            var dto = await BuildStockResponse(productoId, adminId.Value);
            if (dto == null) return NotFound();

            return Ok(dto);
        }

        [HttpPost("Productos/{productoId}/stock/ajustar")]
        public async Task<IActionResult> AjustarStock(int productoId, [FromBody] StockAjustarRequestDTO request)
        {
            var adminId = ObtenerAdminId();
            if (adminId == null) return Unauthorized();

            try
            {
                await _stockService.AjustarStock(
                    productoId,
                    request.VarianteId,
                    request.NuevoStock,
                    request.Motivo ?? "Ajuste manual",
                    adminId.Value);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            var dto = await BuildStockResponse(productoId, adminId.Value);
            if (dto == null) return NotFound();

            return Ok(dto);
        }

        [HttpPost("Productos/{productoId}/stock/agregar")]
        public async Task<IActionResult> AgregarStock(int productoId, [FromBody] StockAgregarRequestDTO request)
        {
            var adminId = ObtenerAdminId();
            if (adminId == null) return Unauthorized();

            try
            {
                await _stockService.ReponerStock(
                    productoId,
                    request.VarianteId,
                    request.Cantidad,
                    request.Motivo ?? "Reposición manual",
                    adminId.Value);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }

            var dto = await BuildStockResponse(productoId, adminId.Value);
            if (dto == null) return NotFound();

            return Ok(dto);
        }

        [HttpGet("Stock/alertas")]
        public async Task<IActionResult> ObtenerAlertas()
        {
            var adminId = ObtenerAdminId();
            if (adminId == null) return Unauthorized();

            var umbral = await _stockRepository.ObtenerUmbralAlerta(adminId.Value);

            var productosTask = _stockRepository.ObtenerProductosStockBajo(adminId.Value, umbral);
            var variantesTask = _stockRepository.ObtenerVariantesStockBajo(adminId.Value, umbral);

            await Task.WhenAll(productosTask, variantesTask);

            var alertas = new List<StockAlertaDTO>();

            foreach (var p in productosTask.Result)
            {
                alertas.Add(new StockAlertaDTO
                {
                    ProductoId = p.Id,
                    NombreProducto = p.Nombre,
                    VarianteId = null,
                    VarianteDescripcion = null,
                    StockActual = p.Stock!.Value,
                    Tipo = p.Stock == 0 ? "agotado" : "bajo"
                });
            }

            foreach (var v in variantesTask.Result)
            {
                alertas.Add(new StockAlertaDTO
                {
                    ProductoId = v.ProductoId,
                    NombreProducto = v.Producto.Nombre,
                    VarianteId = v.Id,
                    VarianteDescripcion = DescripcionVariante(v),
                    StockActual = v.Stock!.Value,
                    Tipo = v.Stock == 0 ? "agotado" : "bajo"
                });
            }

            return Ok(alertas);
        }

        private async Task<StockResponseDTO?> BuildStockResponse(int productoId, int adminId)
        {
            var productoTask = _stockRepository.ObtenerProductoParaStock(productoId, adminId);
            var movimientosTask = _stockRepository.ObtenerUltimosMovimientos(productoId, adminId, 20);

            await Task.WhenAll(productoTask, movimientosTask);

            var producto = productoTask.Result;
            if (producto == null) return null;

            var dto = new StockResponseDTO
            {
                ProductoId = producto.Id,
                NombreProducto = producto.Nombre,
                TieneVariantes = producto.TieneVariantes,
                StockProducto = producto.TieneVariantes ? null : producto.Stock,
                Variantes = producto.TieneVariantes
                    ? producto.Variantes.Select(v => new VarianteStockDTO
                    {
                        VarianteId = v.Id,
                        Descripcion = DescripcionVariante(v),
                        Stock = v.Stock,
                        Disponible = v.Disponible
                    }).ToList()
                    : null,
                UltimosMovimientos = movimientosTask.Result.Select(m => new MovimientoStockDTO
                {
                    Id = m.Id,
                    Tipo = m.Tipo,
                    Cantidad = m.Cantidad,
                    StockAnterior = m.StockAnterior,
                    StockNuevo = m.StockNuevo,
                    Motivo = m.Motivo,
                    FechaCreacion = m.FechaCreacion
                }).ToList()
            };

            return dto;
        }

        private static string DescripcionVariante(VarianteProducto v)
            => v.Opcion2 != null
                ? $"{v.Opcion1.Valor} / {v.Opcion2.Valor}"
                : v.Opcion1.Valor;

        private int? ObtenerAdminId()
        {
            var claim = User.FindFirst("adminId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
