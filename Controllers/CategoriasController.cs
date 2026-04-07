using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vinto.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaService _categoriaService;

        public CategoriasController(ICategoriaService categoriaService)
        {
            _categoriaService = categoriaService;
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

            var categorias = await _categoriaService.ObtenerPorAdministradorId(adminId);
            return Ok(categorias);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategoria(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var categoria = await _categoriaService.ObtenerPorId(id);
            if (categoria == null) return NotFound();
            if (categoria.AdministradorId != adminId) return Forbid();

            return Ok(categoria);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CategoriaCreateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var categoria = new Categoria
            {
                Nombre = dto.Nombre,
                AdministradorId = adminId,
                Administrador = null!,
                Productos = null!
            };

            await _categoriaService.Crear(categoria);
            return CreatedAtAction(nameof(GetCategoria), new { id = categoria.Id }, categoria);
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

            await _categoriaService.Actualizar(categoria);
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

            await _categoriaService.Eliminar(id);
            return NoContent();
        }
    }
}
