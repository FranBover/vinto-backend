using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Controllers
{
    [Authorize]
    [Route("api/tipos-variante/{tipoId}/opciones")]
    [ApiController]
    public class OpcionVarianteController : ControllerBase
    {
        private readonly IOpcionVarianteService _opcionVarianteService;
        private readonly ITipoVarianteService _tipoVarianteService;
        private readonly IProductoService _productoService;

        public OpcionVarianteController(
            IOpcionVarianteService opcionVarianteService,
            ITipoVarianteService tipoVarianteService,
            IProductoService productoService)
        {
            _opcionVarianteService = opcionVarianteService;
            _tipoVarianteService = tipoVarianteService;
            _productoService = productoService;
        }

        private bool TryGetAdminId(out int adminId)
        {
            adminId = 0;
            var claim = User.FindFirst("adminId")?.Value;
            return claim != null && int.TryParse(claim, out adminId);
        }

        /// Carga el TipoVariante y verifica que pertenece al admin autenticado.
        /// Devuelve null si no existe o no pertenece al admin.
        private async Task<TipoVariante?> ObtenerTipoVerificado(int tipoId, int adminId)
        {
            var tipo = await _tipoVarianteService.ObtenerPorId(tipoId);
            if (tipo == null) return null;

            var producto = await _productoService.ObtenerPorId(tipo.ProductoId);
            if (producto == null || producto.AdministradorId != adminId) return null;

            return tipo;
        }

        // GET api/tipos-variante/{tipoId}/opciones
        [HttpGet]
        public async Task<IActionResult> GetOpciones(int tipoId)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var tipo = await ObtenerTipoVerificado(tipoId, adminId);
            if (tipo == null) return NotFound("Tipo de variante no encontrado.");

            var opciones = await _opcionVarianteService.ObtenerPorTipoVarianteId(tipoId);
            return Ok(opciones);
        }

        // POST api/tipos-variante/{tipoId}/opciones
        [HttpPost]
        public async Task<IActionResult> Post(int tipoId, [FromBody] OpcionVarianteCreateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var tipo = await ObtenerTipoVerificado(tipoId, adminId);
            if (tipo == null) return NotFound("Tipo de variante no encontrado.");

            if (await _opcionVarianteService.ExisteValorEnTipo(tipoId, dto.Valor))
                return BadRequest($"Ya existe una opción con el valor '{dto.Valor}' en este tipo de variante.");

            var opcion = new OpcionVariante
            {
                TipoVarianteId = tipoId,
                Valor = dto.Valor,
                Orden = dto.Orden
            };

            await _opcionVarianteService.Crear(opcion);

            var response = new OpcionVarianteResponseDTO
            {
                Id = opcion.Id,
                Valor = opcion.Valor,
                Orden = opcion.Orden,
                TipoVarianteId = opcion.TipoVarianteId
            };

            return CreatedAtAction(nameof(GetOpciones), new { tipoId }, response);
        }

        // PUT api/tipos-variante/{tipoId}/opciones/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int tipoId, int id, [FromBody] OpcionVarianteUpdateDTO dto)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var tipo = await ObtenerTipoVerificado(tipoId, adminId);
            if (tipo == null) return NotFound("Tipo de variante no encontrado.");

            var opcion = await _opcionVarianteService.ObtenerPorId(id);
            if (opcion == null) return NotFound("Opción de variante no encontrada.");
            if (opcion.TipoVarianteId != tipoId) return NotFound("Opción de variante no encontrada.");

            if (await _opcionVarianteService.ExisteValorEnTipo(tipoId, dto.Valor, excludeId: id))
                return BadRequest($"Ya existe una opción con el valor '{dto.Valor}' en este tipo de variante.");

            opcion.Valor = dto.Valor;
            opcion.Orden = dto.Orden;

            await _opcionVarianteService.Actualizar(opcion);
            return NoContent();
        }

        // DELETE api/tipos-variante/{tipoId}/opciones/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int tipoId, int id)
        {
            if (!TryGetAdminId(out int adminId))
                return Forbid();

            var tipo = await ObtenerTipoVerificado(tipoId, adminId);
            if (tipo == null) return NotFound("Tipo de variante no encontrado.");

            var opcion = await _opcionVarianteService.ObtenerPorId(id);
            if (opcion == null) return NotFound("Opción de variante no encontrada.");
            if (opcion.TipoVarianteId != tipoId) return NotFound("Opción de variante no encontrada.");

            if (await _opcionVarianteService.TieneVariantesAsociadas(id))
                return BadRequest("No se puede eliminar la opción porque hay variantes de producto que la utilizan.");

            await _opcionVarianteService.Eliminar(id);
            return NoContent();
        }
    }
}
