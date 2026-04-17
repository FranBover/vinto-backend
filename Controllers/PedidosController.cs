using System.Linq;
using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Vinto.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PedidosController : ControllerBase
    {
        private readonly IPedidoService _pedidoService;
        private readonly AppDbContext _context;




        public PedidosController(IPedidoService pedidoService, AppDbContext context)
        {
            _pedidoService = pedidoService;
            _context = context;
        }


        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? estado,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] string? formaPago,
            [FromQuery] string? formaEntrega)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !int.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var pedidos = await _pedidoService.ObtenerFiltrados(adminId, estado, desde, hasta, formaPago, formaEntrega);

            var resultado = pedidos.Select(p => new PedidoListItemResponseDTO
            {
                Id = p.Id,
                Fecha = p.Fecha,
                Estado = p.Estado,
                NombreCliente = p.NombreCliente,
                FormaPago = p.FormaPago,
                FormaEntrega = p.FormaEntrega,
                Total = p.Total,
                ItemsCount = p.Detalles.Count()
            }).ToList();

            return Ok(resultado);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !int.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.ProductosExtra)
                        .ThenInclude(e => e.ProductoExtra)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.VarianteProducto)
                        .ThenInclude(v => v!.Opcion1)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.VarianteProducto)
                        .ThenInclude(v => v!.Opcion2)
                .FirstOrDefaultAsync(p => p.Id == id && p.AdministradorId == adminId);

            if (pedido == null)
                return NotFound();

            var dto = new PedidoDetailResponseDTO
            {
                Id = pedido.Id,
                Estado = pedido.Estado,
                Fecha = pedido.Fecha,

                NombreCliente = pedido.NombreCliente,
                TelefonoCliente = pedido.TelefonoCliente,

                FormaPago = pedido.FormaPago,
                MontoPagoEfectivo = pedido.MontoPagoEfectivo,
                FormaEntrega = pedido.FormaEntrega,

                DireccionCliente = pedido.DireccionCliente,
                ReferenciaDireccion = pedido.ReferenciaDireccion,
                UbicacionUrl = pedido.UbicacionUrl,

                Total = pedido.Total,
                Detalles = pedido.Detalles.Select(d => new PedidoDetalleResponseDTO
                {
                    NombreProducto = d.Producto?.Nombre ?? string.Empty,
                    VarianteDescripcion = d.VarianteProducto != null
                        ? (d.VarianteProducto.Opcion2 != null
                            ? $"{d.VarianteProducto.Opcion1.Valor} / {d.VarianteProducto.Opcion2.Valor}"
                            : d.VarianteProducto.Opcion1.Valor)
                        : null,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Extras = d.ProductosExtra.Select(e => new PedidoDetalleExtraResponseDTO
                    {
                        Nombre = e.ProductoExtra?.Nombre ?? string.Empty,
                        PrecioAdicional = e.ProductoExtra?.PrecioAdicional ?? 0m
                    }).ToList()
                }).ToList()
            };

            return Ok(dto);
        }

        [Authorize]
        [HttpPatch("{id}/estado")]
        public async Task<IActionResult> PatchEstado(int id, [FromBody] PedidoEstadoUpdateDTO dto)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !int.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var estadosValidos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Pendiente",
                "Confirmado",
                "EnPreparacion",
                "Listo",
                "Entregado",
                "Cancelado"
            };

            if (string.IsNullOrWhiteSpace(dto.Estado) || !estadosValidos.Contains(dto.Estado))
                return BadRequest("Estado inválido.");

            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p => p.Id == id && p.AdministradorId == adminId);

            if (pedido == null)
                return NotFound();

            pedido.Estado = dto.Estado.Trim();
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Pedido pedido)
        {
            if (id != pedido.Id)
                return BadRequest();

            await _pedidoService.Actualizar(pedido);
            return NoContent();
        }


        [HttpPost("/api/public/locales/{slug}/pedidos")]
        public async Task<IActionResult> CrearPedidoPublico(string slug, [FromBody] PedidoPublicCreateRequestDTO request)
        {
            try
            {
                var response = await _pedidoService.CrearPublicoPorSlug(slug, request);
                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                var mensaje = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, $"Error al crear el pedido: {mensaje}");
                
            }
        }

        [Authorize]
        [HttpGet("{id}/resumen")]
        public async Task<IActionResult> ObtenerResumenWhatsApp(int id)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !int.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var resumen = await _pedidoService.ObtenerResumenWhatsAppAdmin(id, adminId);
            if (resumen == null)
                return NotFound("Pedido no encontrado");

            return Ok(new { resumen });
        }

        [Authorize]
        [HttpGet("{id}/comanda")]
        public async Task<IActionResult> GetComanda(int id)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !int.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var comanda = await _pedidoService.GetComandaAsync(id, adminId);
            if (comanda == null)
                return NotFound("Pedido no encontrado");

            return Ok(comanda);
        }

        [Authorize]
        [HttpGet("{id}/ticket")]
        public async Task<IActionResult> GetTicket(int id)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !int.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var ticket = await _pedidoService.GetTicketAsync(id, adminId);
            if (ticket == null)
                return NotFound("Pedido no encontrado");

            return Ok(ticket);
        }

        [Authorize]
        [HttpGet("{id}/comentarios")]
        public async Task<IActionResult> GetComentarios(int id)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !int.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var comentarios = await _pedidoService.GetComentariosAsync(id, adminId);
            if (comentarios == null)
                return NotFound("Pedido no encontrado");

            var resultado = comentarios.Select(c => new ComentarioPedidoResponseDTO
            {
                Id = c.Id,
                Texto = c.Texto,
                FechaCreacion = c.FechaCreacion
            }).ToList();

            return Ok(resultado);
        }

        [Authorize]
        [HttpPost("{id}/comentarios")]
        public async Task<IActionResult> AddComentario(int id, [FromBody] ComentarioPedidoCreateDTO dto)
        {
            var adminIdClaim = User.FindFirst("adminId")?.Value;
            if (string.IsNullOrWhiteSpace(adminIdClaim) || !int.TryParse(adminIdClaim, out var adminId))
                return Unauthorized();

            var comentario = await _pedidoService.AddComentarioAsync(id, adminId, dto.Texto);
            if (comentario == null)
                return NotFound("Pedido no encontrado");

            return Ok(new ComentarioPedidoResponseDTO
            {
                Id = comentario.Id,
                Texto = comentario.Texto,
                FechaCreacion = comentario.FechaCreacion
            });
        }
    }
}