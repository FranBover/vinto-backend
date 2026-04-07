using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Implementaciones;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Vinto.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DetallePedidoController : ControllerBase
    {
        private readonly IDetallePedidoService _detallePedidoService;
        private readonly IDetallePedidoExtraService _detallePedidoExtraService;

        public DetallePedidoController(
            IDetallePedidoService detallePedidoService,
            IDetallePedidoExtraService detallePedidoExtraService)
        {
            _detallePedidoService = detallePedidoService;
            _detallePedidoExtraService = detallePedidoExtraService;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var detalles = await _detallePedidoService.ObtenerTodos();
            return Ok(detalles);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var detalle = await _detallePedidoService.ObtenerPorId(id);
            if (detalle == null)
                return NotFound();

            return Ok(detalle);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] DetallePedidoDTO dto)
        {
            // Crear el DetallePedido
            var detalle = new DetallePedido
            {
                ProductoId = dto.ProductoId,
                Cantidad = dto.Cantidad,
                PrecioUnitario = dto.PrecioUnitario
            };

            await _detallePedidoService.Crear(detalle);

            // Si hay extras, asociarlos al detalle creado
            if (dto.ExtrasSeleccionados != null && dto.ExtrasSeleccionados.Any())
            {
                foreach (var extraId in dto.ExtrasSeleccionados)
                {
                    var detalleExtra = new DetallePedidoExtra
                    {
                        DetallePedidoId = detalle.Id,
                        ProductoExtraId = extraId
                    };

                    await _detallePedidoExtraService.Crear(detalleExtra);
                }
            }

            return CreatedAtAction(nameof(Get), new { id = detalle.Id }, detalle);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] DetallePedido detalle)
        {
            if (id != detalle.Id)
                return BadRequest();

            await _detallePedidoService.Actualizar(detalle);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // Primero eliminamos los extras asociados
            await _detallePedidoExtraService.EliminarPorDetallePedidoId(id);

            // Luego eliminamos el detalle
            await _detallePedidoService.Eliminar(id);

            return NoContent();
        }
    }
}