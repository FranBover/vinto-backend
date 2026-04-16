using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Controllers
{
    [Authorize]
    [Route("api/Productos/{productoId}/tipos-variante")]
    [ApiController]
    public class TipoVarianteController : ControllerBase
    {
        private readonly ITipoVarianteService _tipoVarianteService;
        private readonly IProductoService _productoService;

        public TipoVarianteController(
            ITipoVarianteService tipoVarianteService,
            IProductoService productoService)
        {
            _tipoVarianteService = tipoVarianteService;
            _productoService = productoService;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        // GET api/Productos/{productoId}/tipos-variante
        [HttpGet]
        public async Task<IActionResult> GetTipos(int productoId)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(productoId);
            if (producto == null) return NotFound("Producto no encontrado.");
            if (producto.AdministradorId != adminId) return Forbid();

            var tipos = await _tipoVarianteService.ObtenerPorProductoId(productoId);
            return Ok(tipos);
        }

        // POST api/Productos/{productoId}/tipos-variante
        [HttpPost]
        public async Task<IActionResult> Post(int productoId, [FromBody] TipoVarianteCreateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(productoId);
            if (producto == null) return NotFound("Producto no encontrado.");
            if (producto.AdministradorId != adminId) return Forbid();

            var count = await _tipoVarianteService.ContarPorProductoId(productoId);
            if (count >= 2)
                return BadRequest("Un producto no puede tener más de 2 tipos de variante.");

            var tipo = new TipoVariante
            {
                ProductoId = productoId,
                Nombre = dto.Nombre,
                Orden = dto.Orden
            };

            await _tipoVarianteService.Crear(tipo);

            var response = new TipoVarianteResponseDTO
            {
                Id = tipo.Id,
                Nombre = tipo.Nombre,
                Orden = tipo.Orden,
                CantidadOpciones = 0
            };

            return CreatedAtAction(nameof(GetTipos), new { productoId }, response);
        }

        // PUT api/Productos/{productoId}/tipos-variante/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int productoId, int id, [FromBody] TipoVarianteUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(productoId);
            if (producto == null) return NotFound("Producto no encontrado.");
            if (producto.AdministradorId != adminId) return Forbid();

            var tipo = await _tipoVarianteService.ObtenerPorId(id);
            if (tipo == null) return NotFound("Tipo de variante no encontrado.");
            if (tipo.ProductoId != productoId) return NotFound("Tipo de variante no encontrado.");

            tipo.Nombre = dto.Nombre;
            tipo.Orden = dto.Orden;

            await _tipoVarianteService.Actualizar(tipo);
            return NoContent();
        }

        // DELETE api/Productos/{productoId}/tipos-variante/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int productoId, int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var producto = await _productoService.ObtenerPorId(productoId);
            if (producto == null) return NotFound("Producto no encontrado.");
            if (producto.AdministradorId != adminId) return Forbid();

            var tipo = await _tipoVarianteService.ObtenerPorId(id);
            if (tipo == null) return NotFound("Tipo de variante no encontrado.");
            if (tipo.ProductoId != productoId) return NotFound("Tipo de variante no encontrado.");

            await _tipoVarianteService.Eliminar(id, productoId);
            return NoContent();
        }
    }
}
