using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Eat_Experience.Services.Interfaces;
using Eat_Experience.Models;
using Microsoft.AspNetCore.Authorization;

namespace Eat_Experience.Controllers
{

    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AdministradorController : ControllerBase
    {
        private readonly IAdministradorService _service;

        public AdministradorController(IAdministradorService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var admins = await _service.ObtenerTodos();
            return Ok(admins);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var admin = await _service.ObtenerPorId(id);
            if (admin == null)
                return NotFound();

            return Ok(admin);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Administrador administrador)
        {
            await _service.Crear(administrador);
            return CreatedAtAction(nameof(Get), new { id = administrador.Id }, administrador);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Administrador administrador)
        {
            if (id != administrador.Id)
                return BadRequest();

            await _service.Actualizar(administrador);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.Eliminar(id);
            return NoContent();
        }
    }
}
