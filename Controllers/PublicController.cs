using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Vinto.Api.Controllers
{
    [ApiController]
    [Route("api/public")]
    public class PublicController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PublicController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("locales/{slug}/menu")]
        public async Task<IActionResult> GetMenu(string slug)
        {
            var administrador = await _context.Administradores
                .FirstOrDefaultAsync(a =>
                    a.NombreLocal.ToLower().Replace(" ", "-") == slug);

            if (administrador == null)
                return NotFound();

            var categorias = await _context.Categorias
                .Where(c => c.AdministradorId == administrador.Id)
                .Include(c => c.Productos.Where(p => p.Disponible))
                    .ThenInclude(p => p.Extras)
                .ToListAsync();

            var response = new MenuPublicoResponseDTO
            {
                Local = new LocalInfoDTO
                {
                    NombreLocal = administrador.NombreLocal,
                    Telefono = administrador.Telefono,
                    LinkWhatsapp = administrador.LinkWhatsapp,
                    LogoUrl = administrador.LogoUrl,
                    Direccion = administrador.Direccion,
                    EsActivo = administrador.EsActivo,
                    AliasTransferencia = administrador.AliasTransferencia,
                    TitularCuenta = administrador.TitularCuenta,
                    Horarios = administrador.Horarios,
                    UbicacionUrl = administrador.UbicacionUrl
                },
                Categorias = categorias.Select(c => new CategoriaMenuDTO
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Productos = c.Productos.Select(p => new ProductoMenuDTO
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        Precio = p.Precio,
                        ImagenUrl = p.ImagenUrl,
                        Disponible = p.Disponible,
                        Extras = p.Extras.Select(e => new ProductoExtraMenuDTO
                        {
                            Id = e.Id,
                            Nombre = e.Nombre,
                            PrecioAdicional = e.PrecioAdicional
                        }).ToList()
                    }).ToList()
                }).ToList()
            };

            return Ok(response);
        }
    }
}
