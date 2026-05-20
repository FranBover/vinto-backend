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
    public class CuponesController : ControllerBase
    {
        private readonly ICuponService _cuponService;

        public CuponesController(ICuponService cuponService)
        {
            _cuponService = cuponService;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        [HttpGet]
        public async Task<IActionResult> GetCupones([FromQuery] bool? activo = null)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var cupones = await _cuponService.GetAllAsync(adminId, activo);
            return Ok(cupones);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCupon(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var cupon = await _cuponService.GetByIdAsync(id, adminId);
            if (cupon == null)
                return NotFound();

            return Ok(cupon);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CuponCreateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            try
            {
                var resultado = await _cuponService.CreateAsync(dto, adminId);
                return CreatedAtAction(nameof(GetCupon), new { id = resultado.Id }, resultado);
            }
            catch (ValidacionException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] CuponUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            try
            {
                var resultado = await _cuponService.UpdateAsync(id, dto, adminId);
                if (resultado == null)
                    return NotFound();

                return Ok(resultado);
            }
            catch (ValidacionException ex)
            {
                return BadRequest(new { mensaje = ex.Message });
            }
        }

        [HttpGet("{id}/metricas")]
        public async Task<IActionResult> GetMetricas(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var metricas = await _cuponService.GetMetricasAsync(id, adminId);
            if (metricas == null)
                return NotFound();

            return Ok(metricas);
        }

        [AllowAnonymous]
        [HttpPost("/api/public/locales/{slug}/cupones/validar")]
        public async Task<IActionResult> ValidarCuponPublico(string slug, [FromBody] ValidarCuponRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Codigo))
                return BadRequest(new { mensaje = "El código del cupón es obligatorio" });

            if (request.SubtotalPostDescuentos <= 0)
                return BadRequest(new { mensaje = "El subtotal debe ser mayor a 0" });

            try
            {
                var resultado = await _cuponService.ValidarCuponPublicoAsync(slug, request);
                return Ok(resultado);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mensaje = ex.Message });
            }
        }
    }
}
