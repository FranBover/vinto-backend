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
    public class ProductosController : ControllerBase
    {
        private readonly IProductoService _productoService;

        public ProductosController(IProductoService productoService)
        {
            _productoService = productoService;
        }

        [HttpGet]
        public async Task<IActionResult> GetProductos()
        {
            var productos = await _productoService.ObtenerTodos();
            var response = productos.Select(MapToResponseDto);
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProducto(int id)
        {
            var producto = await _productoService.ObtenerPorId(id);
            if (producto == null) return NotFound();

            return Ok(MapToResponseDto(producto));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ProductoCreateDTO dto)
        {
            var producto = new Producto
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                Precio = dto.Precio,
                ImagenUrl = dto.ImagenUrl,
                Disponible = dto.Disponible,
                CategoriaId = dto.CategoriaId,
                AdministradorId = dto.AdministradorId,
                Categoria = null!,
                Administrador = null!,
                Extras = null!
            };

            await _productoService.Crear(producto);
            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, MapToResponseDto(producto));
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ProductoUpdateDTO dto)
        {
            var producto = await _productoService.ObtenerPorId(id);
            if (producto == null) return NotFound();

            producto.Nombre = dto.Nombre;
            producto.Descripcion = dto.Descripcion;
            producto.Precio = dto.Precio;
            producto.ImagenUrl = dto.ImagenUrl;
            producto.Disponible = dto.Disponible;
            producto.CategoriaId = dto.CategoriaId;
            // AdministradorId nunca se modifica en un update

            await _productoService.Actualizar(producto);
            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _productoService.Eliminar(id);
            return NoContent();
        }

        private static ProductoResponseDTO MapToResponseDto(Producto producto)
        {
            return new ProductoResponseDTO
            {
                Id = producto.Id,
                Nombre = producto.Nombre,
                Descripcion = producto.Descripcion,
                Precio = producto.Precio,
                ImagenUrl = producto.ImagenUrl,
                Disponible = producto.Disponible,
                CategoriaId = producto.CategoriaId,
                AdministradorId = producto.AdministradorId
            };
        }

    }
}
