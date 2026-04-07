using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Vinto.Api.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class DetallePedidoExtraController : ControllerBase
    {
        private readonly IDetallePedidoExtraService _detallePedidoExtraService;

        public DetallePedidoExtraController(IDetallePedidoExtraService detallePedidoExtraService)
        {
            _detallePedidoExtraService = detallePedidoExtraService;
        }

        // GET: api/DetallePedidoExtra/detalle/5
        [HttpGet("detalle/{detallePedidoId}")]
        public async Task<IActionResult> ObtenerPorDetallePedidoId(int detallePedidoId)
        {
            var extras = await _detallePedidoExtraService.ObtenerPorDetallePedidoId(detallePedidoId);
            return Ok(extras);
        }

        // POST: api/DetallePedidoExtra
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] DetallePedidoExtra detallePedidoExtra)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _detallePedidoExtraService.Crear(detallePedidoExtra);
            return Ok(detallePedidoExtra);
        }

        // DELETE: api/DetallePedidoExtra/detalle/5
        [HttpDelete("detalle/{detallePedidoId}")]
        public async Task<IActionResult> EliminarPorDetallePedidoId(int detallePedidoId)
        {
            await _detallePedidoExtraService.EliminarPorDetallePedidoId(detallePedidoId);
            return NoContent();
        }
    }
}
