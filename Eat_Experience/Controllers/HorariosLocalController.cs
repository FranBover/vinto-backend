using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Controllers
{
    // Horarios y cierre manual del local del admin logueado ("Mi local"). El local sale siempre del claim
    // adminId: ninguna ruta ni DTO lleva un id de administrador. Comparte el prefijo api/Administrador/local
    // con PATCH api/Administrador/{id}/local.
    [Authorize]
    [Route("api/Administrador/local")]
    [ApiController]
    public class HorariosLocalController : ControllerBase
    {
        private readonly IHorarioLocalService _service;

        public HorariosLocalController(IHorarioLocalService service)
        {
            _service = service;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        // GET api/Administrador/local/horarios
        [HttpGet("horarios")]
        public async Task<IActionResult> GetHorarios()
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var respuesta = await _service.ObtenerAsync(adminId);
            return respuesta == null ? NotFound() : Ok(respuesta);
        }

        // PUT api/Administrador/local/horarios
        [HttpPut("horarios")]
        public async Task<IActionResult> PutHorarios([FromBody] HorariosLocalUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            try
            {
                var respuesta = await _service.ReemplazarAsync(adminId, dto);
                return respuesta == null ? NotFound() : Ok(respuesta);
            }
            catch (HorariosInvalidosException ex)
            {
                return BadRequest(new { mensaje = ex.Message, errores = ex.Errores });
            }
            catch (ValidacionException ex)
            {
                return StatusCode(ex.StatusCode, new { mensaje = ex.Message });
            }
        }

        // PATCH api/Administrador/local/cierre-manual   { "cerrado": true|false }
        [HttpPatch("cierre-manual")]
        public async Task<IActionResult> PatchCierreManual([FromBody] CierreManualUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            if (dto.Cerrado is null)
                return BadRequest(new { mensaje = "Indicá si el local queda cerrado (cerrado: true) o abierto (cerrado: false)." });

            var estado = await _service.FijarCierreManualAsync(adminId, dto.Cerrado.Value);
            return estado == null ? NotFound() : Ok(estado);
        }
    }
}
