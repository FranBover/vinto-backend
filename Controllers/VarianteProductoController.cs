using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vinto.Api.DTOs;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Controllers
{
    [Authorize]
    [ApiController]
    public class VarianteProductoController : ControllerBase
    {
        private readonly IVarianteProductoService _varianteService;
        private readonly IProductoService _productoService;

        public VarianteProductoController(
            IVarianteProductoService varianteService,
            IProductoService productoService)
        {
            _varianteService = varianteService;
            _productoService = productoService;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        // GET api/Productos/{productoId}/variantes
        [HttpGet("api/Productos/{productoId}/variantes")]
        public async Task<IActionResult> GetVariantes(int productoId)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(productoId);
            if (producto == null) return NotFound("Producto no encontrado.");
            if (producto.AdministradorId != adminId) return Forbid();

            var variantes = await _varianteService.ObtenerPorProductoId(productoId);
            return Ok(variantes);
        }

        // POST api/Productos/{productoId}/variantes/generar
        [HttpPost("api/Productos/{productoId}/variantes/generar")]
        public async Task<IActionResult> Generar(int productoId)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(productoId);
            if (producto == null) return NotFound("Producto no encontrado.");
            if (producto.AdministradorId != adminId) return Forbid();

            var (error, variantes) = await _varianteService.Generar(productoId);
            if (error != null)
                return BadRequest(error);

            return Ok(variantes);
        }

        // PUT api/Variantes/{varianteId}
        [HttpPut("api/Variantes/{varianteId}")]
        public async Task<IActionResult> Put(int varianteId, [FromBody] VarianteProductoUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var variante = await _varianteService.ObtenerPorId(varianteId);
            if (variante == null) return NotFound("Variante no encontrada.");

            var producto = await _productoService.ObtenerPorId(variante.ProductoId);
            if (producto == null || producto.AdministradorId != adminId) return Forbid();

            variante.Precio = dto.Precio;
            variante.Stock = dto.Stock;
            variante.Disponible = dto.Disponible;
            variante.Sku = dto.Sku;

            await _varianteService.Actualizar(variante);
            return NoContent();
        }

        // DELETE api/Productos/{productoId}/variantes
        [HttpDelete("api/Productos/{productoId}/variantes")]
        public async Task<IActionResult> DeleteTodas(int productoId)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(productoId);
            if (producto == null) return NotFound("Producto no encontrado.");
            if (producto.AdministradorId != adminId) return Forbid();

            await _varianteService.EliminarTodas(productoId);
            return NoContent();
        }

        // DELETE api/Variantes/{varianteId}
        [HttpDelete("api/Variantes/{varianteId}")]
        public async Task<IActionResult> Delete(int varianteId)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var variante = await _varianteService.ObtenerPorId(varianteId);
            if (variante == null) return NotFound("Variante no encontrada.");

            var producto = await _productoService.ObtenerPorId(variante.ProductoId);
            if (producto == null || producto.AdministradorId != adminId) return Forbid();

            try
            {
                await _varianteService.Eliminar(varianteId);
            }
            catch (DbUpdateException)
            {
                return BadRequest("No se puede eliminar la variante porque tiene pedidos asociados.");
            }

            return NoContent();
        }
    }
}
