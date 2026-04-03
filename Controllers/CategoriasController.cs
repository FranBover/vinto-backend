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
    public class CategoriasController : ControllerBase
    {
        private readonly ICategoriaService _categoriaService;

        public CategoriasController(ICategoriaService categoriaService)
        {
            _categoriaService = categoriaService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategorias()
        {
            var categorias = await _categoriaService.ObtenerTodas();
            return Ok(categorias);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategoria(int id)
        {
            var categoria = await _categoriaService.ObtenerPorId(id);
            if (categoria == null) return NotFound();

            return Ok(categoria);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CategoriaCreateDTO dto)
        {
            var categoria = new Categoria
            {
                Nombre = dto.Nombre,
                AdministradorId = dto.AdministradorId,
                Administrador = null!,
                Productos = null!
            };

            await _categoriaService.Crear(categoria);
            return CreatedAtAction(nameof(GetCategoria), new { id = categoria.Id }, categoria);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] CategoriaUpdateDTO dto)
        {
            var categoria = new Categoria
            {
                Id = id,
                Nombre = dto.Nombre,
                AdministradorId = dto.AdministradorId,
                Administrador = null!,
                Productos = null!
            };

            await _categoriaService.Actualizar(categoria);
            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _categoriaService.Eliminar(id);
            return NoContent();
        }

    }
}
