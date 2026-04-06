using Eat_Experience.DTOs;
using Eat_Experience.Models;
using Eat_Experience.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eat_Experience.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductoExtraController : ControllerBase
    {
        private readonly IProductoExtraService _productoExtraService;
        private readonly IProductoService _productoService;

        public ProductoExtraController(IProductoExtraService productoExtraService, IProductoService productoService)
        {
            _productoExtraService = productoExtraService;
            _productoService = productoService;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var extras = await _productoExtraService.ObtenerPorAdministradorId(adminId);
            return Ok(extras.Select(MapToResponseDto));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var extra = await _productoExtraService.ObtenerPorId(id);
            if (extra == null) return NotFound();
            if (extra.Producto.AdministradorId != adminId) return Forbid();

            return Ok(MapToResponseDto(extra));
        }

        [HttpGet("por-producto/{productoId}")]
        public async Task<IActionResult> GetByProductoId(int productoId)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(productoId);
            if (producto == null) return NotFound();
            if (producto.AdministradorId != adminId) return Forbid();

            var extras = await _productoExtraService.ObtenerPorProductoId(productoId);
            return Ok(extras.Select(MapToResponseDto));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductoExtraCreateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(dto.ProductoId);
            if (producto == null) return NotFound();
            if (producto.AdministradorId != adminId) return Forbid();

            var extra = new ProductoExtra
            {
                Nombre = dto.Nombre,
                PrecioAdicional = dto.PrecioAdicional,
                ProductoId = dto.ProductoId,
                Producto = null!
            };

            await _productoExtraService.Crear(extra);
            return CreatedAtAction(nameof(GetById), new { id = extra.Id }, MapToResponseDto(extra));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductoExtraUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var extra = await _productoExtraService.ObtenerPorId(id);
            if (extra == null) return NotFound();
            if (extra.Producto.AdministradorId != adminId) return Forbid();

            extra.Nombre = dto.Nombre;
            extra.PrecioAdicional = dto.PrecioAdicional;

            await _productoExtraService.Actualizar(extra);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var extra = await _productoExtraService.ObtenerPorId(id);
            if (extra == null) return NotFound();
            if (extra.Producto.AdministradorId != adminId) return Forbid();

            await _productoExtraService.Eliminar(id);
            return NoContent();
        }

        private static ProductoExtraResponseDTO MapToResponseDto(ProductoExtra extra)
        {
            return new ProductoExtraResponseDTO
            {
                Id = extra.Id,
                Nombre = extra.Nombre,
                PrecioAdicional = extra.PrecioAdicional,
                ProductoId = extra.ProductoId
            };
        }
    }
}
