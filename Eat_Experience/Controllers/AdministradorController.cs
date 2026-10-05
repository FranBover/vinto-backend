using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vinto.Api.Helpers;
using Vinto.Api.Services.Interfaces;
using Vinto.Api.Models;
using Vinto.Api.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace Vinto.Api.Controllers
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

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (adminIdClaim == null || !int.TryParse(adminIdClaim, out int tokenAdminId) || tokenAdminId != id)
                return Forbid();

            var admin = await _service.ObtenerPorId(id);
            if (admin == null)
                return NotFound();

            return Ok(MapToResponseDTO(admin));
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

            // Primero lo único que puede fallar por validación, antes de tocar la entidad.
            // El slug es independiente de NombreLocal: renombrar el local NO cambia la URL pública.
            if (dto.SlugLocal != null)
            {
                try
                {
                    admin.SlugLocal = await _service.ValidarNuevoSlugAsync(id, dto.SlugLocal);
                }
                catch (ValidacionException ex)
                {
                    return BadRequest(new { mensaje = ex.Message });
                }
            }

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
            if (dto.ZonaEnvio != null) admin.ZonaEnvio = dto.ZonaEnvio;
            if (dto.CostoEnvio.HasValue) admin.CostoEnvio = dto.CostoEnvio.Value;

            await _service.Actualizar(admin);
            return Ok(MapToResponseDTO(admin));
        }

        // Mapeo manual entidad -> DTO de respuesta, omitiendo campos sensibles
        // (PasswordHash y tokens/credenciales de MercadoPago).
        private static AdministradorResponseDTO MapToResponseDTO(Administrador admin)
        {
            return new AdministradorResponseDTO
            {
                Id = admin.Id,
                Nombre = admin.Nombre,
                Email = admin.Email,
                NombreLocal = admin.NombreLocal,
                SlugLocal = admin.SlugLocal,
                Direccion = admin.Direccion,
                Telefono = admin.Telefono,
                LinkWhatsapp = admin.LinkWhatsapp,
                LogoUrl = admin.LogoUrl,
                EsActivo = admin.EsActivo,
                FechaRegistro = admin.FechaRegistro,
                UltimoAcceso = admin.UltimoAcceso,
                PlanSuscripcion = admin.PlanSuscripcion,
                DominioPersonalizado = admin.DominioPersonalizado,
                AliasTransferencia = admin.AliasTransferencia,
                TitularCuenta = admin.TitularCuenta,
                Horarios = admin.Horarios,
                UbicacionUrl = admin.UbicacionUrl,
                ZonaEnvio = admin.ZonaEnvio,
                CostoEnvio = admin.CostoEnvio,
                StockBajoAlerta = admin.StockBajoAlerta,
                AutoDeshabilitarSinStock = admin.AutoDeshabilitarSinStock,
                MercadoPagoTokenExpiresAt = admin.MercadoPagoTokenExpiresAt,
                MercadoPagoConectado = admin.MercadoPagoConectado
            };
        }
    }
}
