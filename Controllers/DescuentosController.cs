using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DescuentosController : ControllerBase
    {
        private readonly IDescuentoService _descuentoService;

        public DescuentosController(IDescuentoService descuentoService)
        {
            _descuentoService = descuentoService;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        [HttpGet]
        public async Task<IActionResult> GetDescuentos([FromQuery] bool? activo = null)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var descuentos = await _descuentoService.GetAllAsync(adminId, activo);
            return Ok(descuentos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDescuento(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var descuento = await _descuentoService.GetByIdAsync(id, adminId);
            if (descuento == null)
                return NotFound();

            return Ok(descuento);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] DescuentoCreateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            try
            {
                var resultado = await _descuentoService.CreateAsync(dto, adminId);
                return CreatedAtAction(nameof(GetDescuento), new { id = resultado.Id }, resultado);
            }
            catch (ValidacionException ex) when (ex.StatusCode == 404)
            {
                return NotFound(new { mensaje = ex.Message });
            }
            catch (ValidacionException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] DescuentoUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            try
            {
                var resultado = await _descuentoService.UpdateAsync(id, dto, adminId);
                if (resultado == null)
                    return NotFound();

                return Ok(resultado);
            }
            catch (ValidacionException ex) when (ex.StatusCode == 404)
            {
                return NotFound(new { mensaje = ex.Message });
            }
            catch (ValidacionException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }
    }
}
