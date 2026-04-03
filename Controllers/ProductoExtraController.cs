using Eat_Experience.DTOs;
using Eat_Experience.Models;
using Eat_Experience.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Eat_Experience.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductoExtraController : ControllerBase
    {
        private readonly IProductoExtraService _productoExtraService;

        public ProductoExtraController(IProductoExtraService productoExtraService)
        {
            _productoExtraService = productoExtraService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductoExtraResponseDTO>>> GetAll()
        {
            var extras = await _productoExtraService.ObtenerTodos();
            var response = extras.Select(MapToResponseDto);
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProductoExtraResponseDTO>> GetById(int id)
        {
            var extra = await _productoExtraService.ObtenerPorId(id);
            if (extra == null) return NotFound();
            return Ok(MapToResponseDto(extra));
        }

        [HttpGet("por-producto/{productoId}")]
        public async Task<ActionResult<IEnumerable<ProductoExtra>>> GetByProductoId(int productoId)
        {
            var extras = await _productoExtraService.ObtenerPorProductoId(productoId);
            return Ok(extras);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProductoExtraCreateDTO dto)
        {
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

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductoExtraUpdateDTO dto)
        {
            var extra = new ProductoExtra
            {
                Id = id,
                Nombre = dto.Nombre,
                PrecioAdicional = dto.PrecioAdicional,
                ProductoId = dto.ProductoId,
                Producto = null!
            };

            await _productoExtraService.Actualizar(extra);
            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
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
