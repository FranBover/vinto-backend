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
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaService _categoriaService;
        private readonly IImagenService _imagenService;
        private readonly AppDbContext _context;

        public CategoriasController(
            ICategoriaService categoriaService,
            IImagenService imagenService,
            AppDbContext context)
        {
            _categoriaService = categoriaService;
            _imagenService = imagenService;
            _context = context;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        [HttpGet]
        public async Task<IActionResult> GetCategorias()
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var categorias = (await _categoriaService.ObtenerPorAdministradorId(adminId))
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            var imagenes = await _context.Imagenes
                .Where(i => i.AdministradorId == adminId && i.Tipo == "categoria")
                .OrderBy(i => i.Orden)
                .ToListAsync();

            var imagenPorCategoria = imagenes
                .GroupBy(i => i.EntidadId)
                .ToDictionary(g => g.Key, g => g.First());

            var response = categorias.Select(c => new CategoriaResponseDTO
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Orden = c.Orden,
                ImagenUrl = imagenPorCategoria.TryGetValue(c.Id, out var img) ? img.Url : null
            }).ToList();

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategoria(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var categoria = await _categoriaService.ObtenerPorId(id);
            if (categoria == null) return NotFound();
            if (categoria.AdministradorId != adminId) return Forbid();

            var imagen = await _context.Imagenes
                .Where(i => i.AdministradorId == adminId && i.Tipo == "categoria" && i.EntidadId == id)
                .OrderBy(i => i.Orden)
                .FirstOrDefaultAsync();

            var response = new CategoriaResponseDTO
            {
                Id = categoria.Id,
                Nombre = categoria.Nombre,
                Orden = categoria.Orden,
                ImagenUrl = imagen?.Url
            };

            return Ok(response);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CategoriaCreateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            int orden;
            if (dto.Orden.HasValue)
            {
                orden = dto.Orden.Value;
            }
            else
            {
                var maxOrden = await _context.Categorias
                    .Where(c => c.AdministradorId == adminId)
                    .MaxAsync(c => (int?)c.Orden) ?? 0;
                orden = maxOrden + 1;
            }

            var categoria = new Categoria
            {
                Nombre = dto.Nombre,
                Orden = orden,
                AdministradorId = adminId,
                Administrador = null!,
                Productos = null!
            };

            await _categoriaService.Crear(categoria);

            var response = new CategoriaResponseDTO
            {
                Id = categoria.Id,
                Nombre = categoria.Nombre,
                Orden = categoria.Orden,
                ImagenUrl = null
            };

            return CreatedAtAction(nameof(GetCategoria), new { id = categoria.Id }, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] CategoriaUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var categoria = await _categoriaService.ObtenerPorId(id);
            if (categoria == null) return NotFound();
            if (categoria.AdministradorId != adminId) return Forbid();

            categoria.Nombre = dto.Nombre;
            if (dto.Orden.HasValue)
                categoria.Orden = dto.Orden.Value;

            await _categoriaService.Actualizar(categoria);
            return NoContent();
        }

        [HttpPatch("reordenar")]
        public async Task<IActionResult> Reordenar([FromBody] ReordenarCategoriasRequestDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            if (dto.OrderedIds == null || dto.OrderedIds.Count == 0)
                return BadRequest(new { error = "La lista de IDs no puede estar vacía." });

            if (dto.OrderedIds.Distinct().Count() != dto.OrderedIds.Count)
                return BadRequest(new { error = "Hay IDs duplicados en la lista." });

            var categoriasDelAdmin = await _context.Categorias
                .Where(c => c.AdministradorId == adminId)
                .ToListAsync();

            if (categoriasDelAdmin.Count != dto.OrderedIds.Count)
                return BadRequest(new
                {
                    error = $"La lista debe contener exactamente {categoriasDelAdmin.Count} IDs " +
                            "(todas las categorías del admin)."
                });

            var idsValidos = categoriasDelAdmin.Select(c => c.Id).ToHashSet();
            if (dto.OrderedIds.Any(id => !idsValidos.Contains(id)))
                return BadRequest(new { error = "Hay IDs que no pertenecen al admin o no existen." });

            for (int i = 0; i < dto.OrderedIds.Count; i++)
            {
                var categoria = categoriasDelAdmin.First(c => c.Id == dto.OrderedIds[i]);
                categoria.Orden = i + 1;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var categoria = await _categoriaService.ObtenerPorId(id);
            if (categoria == null) return NotFound();
            if (categoria.AdministradorId != adminId) return Forbid();

            // Borrar imágenes asociadas (si existen) antes de eliminar la categoría
            var imagenes = await _imagenService.GetByEntidadAsync(adminId, "categoria", id);
            foreach (var img in imagenes)
            {
                await _imagenService.DeleteAsync(img.Id, adminId);
            }

            await _categoriaService.Eliminar(id);
            return NoContent();
        }
    }
}
