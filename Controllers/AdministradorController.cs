using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Eat_Experience.Services.Interfaces;
using Eat_Experience.Models;
using Eat_Experience.DTOs;
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

        [HttpPatch("{id}/local")]
        public async Task<IActionResult> PatchLocal(int id, [FromBody] AdministradorLocalUpdateDTO dto)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (adminIdClaim == null || !int.TryParse(adminIdClaim, out int tokenAdminId) || tokenAdminId != id)
                return Forbid();

            var admin = await _service.ObtenerPorId(id);
            if (admin == null)
                return NotFound();

            if (dto.NombreLocal != null) admin.NombreLocal = dto.NombreLocal;
            if (dto.Telefono != null) admin.Telefono = dto.Telefono;
            if (dto.Direccion != null) admin.Direccion = dto.Direccion;
            if (dto.LinkWhatsapp != null) admin.LinkWhatsapp = dto.LinkWhatsapp;
            if (dto.LogoUrl != null) admin.LogoUrl = dto.LogoUrl;
            if (dto.EsActivo.HasValue) admin.EsActivo = dto.EsActivo.Value;
            if (dto.AliasTransferencia != null) admin.AliasTransferencia = dto.AliasTransferencia;
            if (dto.TitularCuenta != null) admin.TitularCuenta = dto.TitularCuenta;
            if (dto.Horarios != null) admin.Horarios = dto.Horarios;
            if (dto.UbicacionUrl != null) admin.UbicacionUrl = dto.UbicacionUrl;

            await _service.Actualizar(admin);
            return Ok(admin);
        }
    }
}
