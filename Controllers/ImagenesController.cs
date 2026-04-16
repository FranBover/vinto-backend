using Vinto.Api.DTOs;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vinto.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ImagenesController : ControllerBase
    {
        private readonly IImagenService _imagenService;

        public ImagenesController(IImagenService imagenService)
        {
            _imagenService = imagenService;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ImagenResponseDTO), 201)]
        public async Task<IActionResult> Upload([FromForm] ImagenUploadRequestDTO request)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            try
            {
                var result = await _imagenService.UploadAsync(
                    request.File, adminId, request.Tipo, request.EntidadId, request.Orden);
                return StatusCode(201, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            try
            {
                await _imagenService.DeleteAsync(id, adminId);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetByEntidad(
            [FromQuery] string tipo,
            [FromQuery] int? entidadId)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var result = await _imagenService.GetByEntidadAsync(adminId, tipo, entidadId);
            return Ok(result);
        }
    }
}
