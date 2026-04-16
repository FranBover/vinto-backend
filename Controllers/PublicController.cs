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
                .Include(c => c.Productos.Where(p => p.Disponible))
                    .ThenInclude(p => p.TiposVariante.OrderBy(t => t.Orden))
                        .ThenInclude(t => t.Opciones.OrderBy(o => o.Orden))
                .Include(c => c.Productos.Where(p => p.Disponible))
                    .ThenInclude(p => p.Variantes)
                        .ThenInclude(v => v.Opcion1)
                .Include(c => c.Productos.Where(p => p.Disponible))
                    .ThenInclude(p => p.Variantes)
                        .ThenInclude(v => v.Opcion2)
                .ToListAsync();

            var imagenes = await _context.Imagenes
                .Where(i => i.AdministradorId == administrador.Id && i.Tipo == "producto")
                .OrderBy(i => i.Orden)
                .ToListAsync();

            var imagenesPorProducto = imagenes
                .GroupBy(i => i.EntidadId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var logoImagen = await _context.Imagenes
                .Where(i => i.AdministradorId == administrador.Id && i.Tipo == "logo")
                .OrderByDescending(i => i.FechaCreacion)
                .FirstOrDefaultAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var logoImagenUrl = logoImagen != null ? baseUrl + logoImagen.Url : null;

            var response = new MenuPublicoResponseDTO
            {
                Local = new LocalInfoDTO
                {
                    NombreLocal = administrador.NombreLocal,
                    Telefono = administrador.Telefono,
                    LinkWhatsapp = administrador.LinkWhatsapp,
                    LogoUrl = administrador.LogoUrl,
                    LogoImagenUrl = logoImagenUrl,
                    Direccion = administrador.Direccion,
                    EsActivo = administrador.EsActivo,
                    AliasTransferencia = administrador.AliasTransferencia,
                    TitularCuenta = administrador.TitularCuenta,
                    Horarios = administrador.Horarios,
                    UbicacionUrl = administrador.UbicacionUrl,
                    ZonaEnvio = administrador.ZonaEnvio,
                    CostoEnvio = administrador.CostoEnvio
                },
                Categorias = categorias.Select(c => new CategoriaMenuDTO
                {
                    Id = c.Id,
                    Nombre = c.Nombre,
                    Productos = c.Productos.Select(p =>
                    {
                        imagenesPorProducto.TryGetValue(p.Id, out var imgs);
                        return new ProductoMenuDTO
                        {
                            Id = p.Id,
                            Nombre = p.Nombre,
                            Descripcion = p.Descripcion,
                            Precio = p.TieneVariantes ? null : p.Precio,
                            ImagenUrl = p.ImagenUrl,
                            Disponible = p.Disponible,
                            TieneVariantes = p.TieneVariantes,
                            Extras = p.Extras.Select(e => new ProductoExtraMenuDTO
                            {
                                Id = e.Id,
                                Nombre = e.Nombre,
                                PrecioAdicional = e.PrecioAdicional
                            }).ToList(),
                            Imagenes = imgs?.Select(i => new ImagenMenuDTO
                            {
                                Url = i.Url,
                                Orden = i.Orden
                            }).ToList() ?? new List<ImagenMenuDTO>(),
                            TiposVariante = p.TieneVariantes
                                ? p.TiposVariante.Select(t => new TipoVarianteMenuDTO
                                {
                                    Id = t.Id,
                                    Nombre = t.Nombre,
                                    Orden = t.Orden,
                                    Opciones = t.Opciones.Select(o => new OpcionVarianteMenuDTO
                                    {
                                        Id = o.Id,
                                        Valor = o.Valor,
                                        Orden = o.Orden
                                    }).ToList()
                                }).ToList()
                                : new List<TipoVarianteMenuDTO>(),
                            Variantes = p.TieneVariantes
                                ? p.Variantes
                                    .Where(v => v.Disponible)
                                    .Select(v => new VarianteMenuDTO
                                    {
                                        Id = v.Id,
                                        Precio = v.Precio,
                                        Stock = v.Stock,
                                        Disponible = v.Disponible,
                                        Opcion1Id = v.Opcion1Id,
                                        Opcion2Id = v.Opcion2Id,
                                        Descripcion = v.Opcion2 != null
                                            ? $"{v.Opcion1.Valor} / {v.Opcion2.Valor}"
                                            : v.Opcion1.Valor
                                    }).ToList()
                                : new List<VarianteMenuDTO>()
                        };
                    }).ToList()
                }).ToList()
            };

            return Ok(response);
        }
    }
}
