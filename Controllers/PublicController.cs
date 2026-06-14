using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Vinto.Api.Controllers
{
    [ApiController]
    [Route("api/public")]
    public class PublicController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDescuentoCalculatorService _calculatorService;
        private readonly IPedidoService _pedidoService;

        public PublicController(AppDbContext context, IDescuentoCalculatorService calculatorService, IPedidoService pedidoService)
        {
            _context = context;
            _calculatorService = calculatorService;
            _pedidoService = pedidoService;
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

            var imagenesProductos = await _context.Imagenes
                .Where(i => i.AdministradorId == administrador.Id && i.Tipo == "producto")
                .OrderBy(i => i.Orden)
                .ToListAsync();

            var imagenesPorProducto = imagenesProductos
                .GroupBy(i => i.EntidadId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var imagenesCategorias = await _context.Imagenes
                .Where(i => i.AdministradorId == administrador.Id && i.Tipo == "categoria")
                .ToListAsync();

            var imagenPorCategoria = imagenesCategorias
                .GroupBy(i => i.EntidadId)
                .ToDictionary(g => g.Key, g => g.First());

            var logoImagen = await _context.Imagenes
                .Where(i => i.AdministradorId == administrador.Id && i.Tipo == "logo")
                .OrderByDescending(i => i.FechaCreacion)
                .FirstOrDefaultAsync();

            // Una sola query para todos los descuentos del admin
            var todosDescuentos = await _context.Descuentos
                .Where(d => d.AdministradorId == administrador.Id)
                .ToListAsync();

            // Filtro de actividad en memoria
            var ahora = DateTime.UtcNow;
            var descuentosActivos = todosDescuentos.Where(d =>
                d.Activo
                && (d.FechaInicio == null || d.FechaInicio <= ahora)
                && (d.FechaFin == null || d.FechaFin >= ahora)
            ).ToList();

            var descuentosGlobales = descuentosActivos.Where(d => d.AplicaAPedidoCompleto).ToList();
            var descuentosNoGlobales = descuentosActivos.Where(d => !d.AplicaAPedidoCompleto).ToList();

            // Diccionarios para lookup O(1) por producto y por categoría
            var descPorProducto = descuentosNoGlobales
                .Where(d => d.ProductoId.HasValue)
                .GroupBy(d => d.ProductoId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var descPorCategoria = descuentosNoGlobales
                .Where(d => d.CategoriaId.HasValue)
                .GroupBy(d => d.CategoriaId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

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
                    CostoEnvio = administrador.CostoEnvio,
                    MercadoPagoHabilitado = administrador.MercadoPagoConectado
                },
                Categorias = categorias
                    .OrderBy(c => c.Orden)
                    .ThenBy(c => c.Id)
                    .Select(c =>
                    {
                        var descCateg = descPorCategoria.GetValueOrDefault(c.Id) ?? new List<Descuento>();
                        var imagenCategoria = imagenPorCategoria.TryGetValue(c.Id, out var imgC) ? imgC : null;

                        return new CategoriaMenuDTO
                        {
                            Id = c.Id,
                            Nombre = c.Nombre,
                            Orden = c.Orden,
                            ImagenUrl = imagenCategoria?.Url,
                            Productos = c.Productos.Select(p =>
                            {
                                imagenesPorProducto.TryGetValue(p.Id, out var imgs);
                                var descProd = descPorProducto.GetValueOrDefault(p.Id) ?? new List<Descuento>();
                                var descLinea = descProd.Concat(descCateg).ToList();
                                return MapProducto(p, c.Id, descLinea, imgs);
                            }).ToList()
                        };
                    }).ToList(),
                DescuentosPedidoCompleto = descuentosGlobales.Select(d => new DescuentoPedidoCompletoMenuDTO
                {
                    Nombre = d.Nombre,
                    Tipo = d.Tipo,
                    Valor = d.Valor
                }).ToList()
            };

            return Ok(response);
        }

        [HttpGet("pedidos/{codigoSeguimiento}/estado-pago")]
        public async Task<IActionResult> ObtenerEstadoPagoPublico(string codigoSeguimiento)
        {
            try
            {
                var resultado = await _pedidoService.ObtenerEstadoPagoPublico(codigoSeguimiento);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error consultando estado del pago.", detalle = ex.Message });
            }
        }

        private ProductoMenuDTO MapProducto(
            Producto p,
            int categoriaId,
            List<Descuento> descuentosLinea,
            List<Imagen>? imgs)
        {
            decimal? precioConDescuento = null;
            int porcentajeDescuento = 0;
            var descuentosAplicadosDto = new List<DescuentoMenuItemDTO>();

            if (!p.TieneVariantes && p.Precio > 0)
            {
                var linea = new LineaParaCalculo
                {
                    ProductoId = p.Id,
                    CategoriaId = categoriaId,
                    PrecioUnitario = p.Precio,
                    Cantidad = 1
                };
                var calc = _calculatorService.CalcularDescuentos(new List<LineaParaCalculo> { linea }, descuentosLinea);
                var lineaResultado = calc.Lineas[0];

                precioConDescuento = lineaResultado.PrecioUnitarioConDescuento;
                porcentajeDescuento = (int)Math.Round(
                    (p.Precio - lineaResultado.PrecioUnitarioConDescuento) / p.Precio * 100,
                    MidpointRounding.AwayFromZero);
                descuentosAplicadosDto = lineaResultado.DescuentosAplicados
                    .Select(da => new DescuentoMenuItemDTO { Nombre = da.NombreDescuento, Tipo = da.TipoAlcance })
                    .ToList();
            }

            var variantes = p.TieneVariantes
                ? p.Variantes.Where(v => v.Disponible).Select(v =>
                {
                    var lineaVar = new LineaParaCalculo
                    {
                        ProductoId = p.Id,
                        CategoriaId = categoriaId,
                        PrecioUnitario = v.Precio,
                        Cantidad = 1
                    };
                    var calcVar = _calculatorService.CalcularDescuentos(new List<LineaParaCalculo> { lineaVar }, descuentosLinea);
                    var lr = calcVar.Lineas[0];

                    return new VarianteMenuDTO
                    {
                        Id = v.Id,
                        Precio = v.Precio,
                        PrecioConDescuento = lr.PrecioUnitarioConDescuento,
                        PorcentajeDescuentoTotal = v.Precio > 0
                            ? (int)Math.Round(
                                (v.Precio - lr.PrecioUnitarioConDescuento) / v.Precio * 100,
                                MidpointRounding.AwayFromZero)
                            : 0,
                        DescuentosAplicados = lr.DescuentosAplicados
                            .Select(da => new DescuentoMenuItemDTO { Nombre = da.NombreDescuento, Tipo = da.TipoAlcance })
                            .ToList(),
                        Stock = v.Stock,
                        Disponible = v.Disponible,
                        Opcion1Id = v.Opcion1Id,
                        Opcion2Id = v.Opcion2Id,
                        Descripcion = v.Opcion2 != null
                            ? $"{v.Opcion1.Valor} / {v.Opcion2.Valor}"
                            : v.Opcion1.Valor
                    };
                }).ToList()
                : new List<VarianteMenuDTO>();

            return new ProductoMenuDTO
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                Precio = p.TieneVariantes ? null : p.Precio,
                PrecioConDescuento = p.TieneVariantes ? null : (precioConDescuento ?? p.Precio),
                PorcentajeDescuentoTotal = porcentajeDescuento,
                DescuentosAplicados = descuentosAplicadosDto,
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
                Variantes = variantes
            };
        }
    }
}
