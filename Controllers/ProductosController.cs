using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Vinto.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductosController : ControllerBase
    {
        private readonly IProductoService _productoService;
        private readonly AppDbContext _context;

        public ProductosController(IProductoService productoService, AppDbContext context)
        {
            _productoService = productoService;
            _context = context;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductos()
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var productos = await _productoService.ObtenerPorAdministradorId(adminId);

            var imagenes = await _context.Imagenes
                .Where(i => i.AdministradorId == adminId && i.Tipo == "producto")
                .OrderBy(i => i.Orden)
                .ToListAsync();

            var imagenesPorProducto = imagenes
                .GroupBy(i => i.EntidadId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return Ok(productos.Select(p =>
            {
                imagenesPorProducto.TryGetValue(p.Id, out var imgs);
                return MapToResponseDto(p, imgs);
            }));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProducto(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(id);
            if (producto == null) return NotFound();
            if (producto.AdministradorId != adminId) return Forbid();

            var imagenes = await _context.Imagenes
                .Where(i => i.AdministradorId == adminId && i.Tipo == "producto" && i.EntidadId == id)
                .OrderBy(i => i.Orden)
                .ToListAsync();

            return Ok(MapToResponseDto(producto, imagenes));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ProductoCreateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = new Producto
            {
                Nombre = dto.Nombre,
                Descripcion = dto.Descripcion,
                Precio = dto.Precio,
                ImagenUrl = dto.ImagenUrl,
                Disponible = dto.Disponible,
                CategoriaId = dto.CategoriaId,
                AdministradorId = adminId,
                Categoria = null!,
                Administrador = null!,
                Extras = null!
            };

            await _productoService.Crear(producto);
            return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, MapToResponseDto(producto, null));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ProductoUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(id);
            if (producto == null) return NotFound();
            if (producto.AdministradorId != adminId) return Forbid();

            producto.Nombre = dto.Nombre;
            producto.Descripcion = dto.Descripcion;
            producto.Precio = dto.Precio;
            producto.ImagenUrl = dto.ImagenUrl;
            producto.Disponible = dto.Disponible;
            producto.CategoriaId = dto.CategoriaId;

            await _productoService.Actualizar(producto);
            return NoContent();
        }

        [HttpPatch("{id}/disponibilidad")]
        public async Task<IActionResult> PatchDisponibilidad(int id, [FromBody] ProductoDisponibilidadDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(id);
            if (producto == null) return NotFound();
            if (producto.AdministradorId != adminId) return Forbid();

            producto.Disponible = dto.Disponible;

            await _productoService.Actualizar(producto);
            return Ok(MapToResponseDto(producto, null));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(id);
            if (producto == null) return NotFound();
            if (producto.AdministradorId != adminId) return Forbid();

            await _productoService.Eliminar(id);
            return NoContent();
        }

        private static ProductoResponseDTO MapToResponseDto(Producto producto, List<Imagen>? imagenes)
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
                AdministradorId = producto.AdministradorId,
                Imagenes = imagenes?.Select(i => new ImagenResponseDTO
                {
                    Id = i.Id,
                    Url = i.Url,
                    Tipo = i.Tipo,
                    EntidadId = i.EntidadId,
                    Orden = i.Orden,
                    NombreOriginal = i.NombreOriginal,
                    TamanioBytes = i.TamanioBytes,
                    FechaCreacion = i.FechaCreacion
                }).ToList() ?? new List<ImagenResponseDTO>()
            };
        }
    }
}
