using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Hubs;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Vinto.Api.Services.Implementaciones
{
    public class PedidoService : IPedidoService
    {
        private readonly AppDbContext _context;
        private readonly IPedidoRepository _pedidoRepository;
        private readonly IHubContext<PedidosHub> _hubContext;

        public PedidoService(IPedidoRepository pedidoRepository, AppDbContext context, IHubContext<PedidosHub> hubContext)
        {
            _pedidoRepository = pedidoRepository;
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IEnumerable<Pedido>> ObtenerTodos()
        {
            return await _pedidoRepository.ObtenerTodos();
        }

        public async Task<Pedido?> ObtenerPorId(int id)
        {
            return await _pedidoRepository.ObtenerPorId(id);
        }

        public async Task Crear(Pedido pedido)
        {
            await _pedidoRepository.Crear(pedido);
        }

        public async Task Actualizar(Pedido pedido)
        {
            await _pedidoRepository.Actualizar(pedido);
        }

        public async Task Eliminar(int id)
        {
            await _pedidoRepository.Eliminar(id);
        }




        
        public async Task<Pedido> CrearConDetalles(PedidoRequestDTO request)
        {
            if (request.Detalles == null || !request.Detalles.Any())
                throw new Exception("El pedido debe tener al menos un producto.");

            var pedido = new Pedido
            {
                AdministradorId = request.AdministradorId,
                NombreCliente = request.NombreCliente,
                TelefonoCliente = request.TelefonoCliente,
                FormaPago = request.FormaPago,
                FormaEntrega = request.FormaEntrega,
                MontoPagoEfectivo = request.MontoPagoEfectivo,
                DireccionCliente = request.DireccionCliente,
                Fecha = DateTime.UtcNow,
                Detalles = new List<DetallePedido>()
            };

            foreach (var detalleDTO in request.Detalles)
            {
                // Validamos existencia del producto
                var producto = await _context.Productos.FindAsync(detalleDTO.ProductoId);
                if (producto == null)
                    throw new Exception($"Producto con ID {detalleDTO.ProductoId} no encontrado.");

                var detalle = new DetallePedido
                {
                    ProductoId = detalleDTO.ProductoId,
                    Cantidad = detalleDTO.Cantidad,
                    PrecioUnitario = producto.Precio,
                    ProductosExtra = new List<DetallePedidoExtra>()
                };

                // Procesar los extras seleccionados (ProductoExtra)
                if (detalleDTO.ExtrasSeleccionados != null)
                {
                    foreach (var extraId in detalleDTO.ExtrasSeleccionados)
                    {
                        var extra = await _context.ProductoExtras.FindAsync(extraId);
                        if (extra == null)
                            continue; // Ignoramos extras inválidos

                        if (detalleDTO.Cantidad <= 0)
                            throw new Exception($"La cantidad del producto {detalleDTO.ProductoId} debe ser mayor a 0.");

                        detalle.ProductosExtra.Add(new DetallePedidoExtra
                        {
                            ProductoExtraId = extraId
                        });
                    }
                }

                pedido.Detalles.Add(detalle);
            }

            // Calcular el total
            decimal total = 0;
            foreach (var d in pedido.Detalles)
            {
                decimal subtotal = d.PrecioUnitario * d.Cantidad;
                foreach (var extra in d.ProductosExtra)
                {
                    var extraInfo = await _context.ProductoExtras.FindAsync(extra.ProductoExtraId);
                    if (extraInfo != null)
                    {
                        subtotal += extraInfo.PrecioAdicional * d.Cantidad;
                    }
                }
                total += subtotal;
            }

            pedido.Total = total;

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            return pedido;
        }

        public async Task<PedidoCreateResponseDTO> CrearPublicoPorSlug(string slug, PedidoPublicCreateRequestDTO request)
        {
            if (request.Detalles == null || !request.Detalles.Any())
                throw new InvalidOperationException("El pedido debe tener al menos un producto.");

            var slugNormalized = slug.Trim().ToLowerInvariant();

            // EF no puede traducir Slugify() a SQL, por eso traemos admins activos y filtramos en memoria.
            var adminsActivos = await _context.Administradores
                .AsNoTracking()
                .Where(a => a.EsActivo)
                .ToListAsync();

            var admin = adminsActivos.FirstOrDefault(a => Slugify(a.NombreLocal) == slugNormalized);

            if (admin == null)
                throw new KeyNotFoundException("Local no encontrado.");

            if (request.FormaPago == "Efectivo" && request.MontoPagoEfectivo == null)
                throw new InvalidOperationException("Debe indicar con cuánto pagará en efectivo.");

            if (request.FormaEntrega == "Delivery" && string.IsNullOrWhiteSpace(request.DireccionCliente))
                throw new InvalidOperationException("Debe indicar la dirección de entrega para el delivery.");

            var pedido = new Pedido
            {
                AdministradorId = admin.Id,
                NombreCliente = request.NombreCliente,
                TelefonoCliente = request.TelefonoCliente,
                FormaPago = request.FormaPago,
                FormaEntrega = request.FormaEntrega,
                MontoPagoEfectivo = request.MontoPagoEfectivo,
                DireccionCliente = request.DireccionCliente,
                ReferenciaDireccion = request.ReferenciaDireccion,
                UbicacionUrl = request.UbicacionUrl,
                Estado = "Pendiente",
                Fecha = DateTime.UtcNow,
                Detalles = new List<DetallePedido>()
            };

            decimal subtotal = 0m;
            decimal costoEnvio = request.FormaEntrega == "Delivery" && admin.CostoEnvio.HasValue
                ? admin.CostoEnvio.Value
                : 0m;

            foreach (var detalleDTO in request.Detalles)
            {
                if (detalleDTO.Cantidad <= 0)
                    throw new InvalidOperationException($"La cantidad del producto {detalleDTO.ProductoId} debe ser mayor a 0.");

                var producto = await _context.Productos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == detalleDTO.ProductoId && p.AdministradorId == admin.Id && p.Disponible);

                if (producto == null)
                    throw new KeyNotFoundException($"Producto con ID {detalleDTO.ProductoId} no encontrado para este local.");

                decimal precioUnitario;
                int? varianteProductoId = null;

                if (producto.TieneVariantes)
                {
                    if (detalleDTO.VarianteProductoId == null)
                        throw new InvalidOperationException($"El producto '{producto.Nombre}' requiere seleccionar una variante.");

                    var variante = await _context.VariantesProducto
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.Id == detalleDTO.VarianteProductoId);

                    if (variante == null || variante.ProductoId != producto.Id)
                        throw new InvalidOperationException($"La variante seleccionada no es válida para el producto '{producto.Nombre}'.");

                    if (!variante.Disponible)
                        throw new InvalidOperationException("La variante seleccionada no está disponible.");

                    precioUnitario = variante.Precio;
                    varianteProductoId = variante.Id;
                }
                else
                {
                    precioUnitario = producto.Precio;
                }

                var detalle = new DetallePedido
                {
                    ProductoId = producto.Id,
                    Cantidad = detalleDTO.Cantidad,
                    PrecioUnitario = precioUnitario,
                    VarianteProductoId = varianteProductoId,
                    ProductosExtra = new List<DetallePedidoExtra>()
                };

                decimal subtotalDetalle = detalle.PrecioUnitario * detalle.Cantidad;

                if (detalleDTO.ExtrasSeleccionados != null && detalleDTO.ExtrasSeleccionados.Any())
                {
                    foreach (var extraId in detalleDTO.ExtrasSeleccionados.Distinct())
                    {
                        var extra = await _context.ProductoExtras
                            .AsNoTracking()
                            .FirstOrDefaultAsync(e => e.Id == extraId);

                        if (extra == null)
                            throw new KeyNotFoundException($"Extra con ID {extraId} no encontrado.");

                        if (extra.ProductoId != producto.Id)
                            throw new InvalidOperationException($"El extra {extraId} no pertenece al producto {producto.Id}.");

                        detalle.ProductosExtra.Add(new DetallePedidoExtra
                        {
                            ProductoExtraId = extraId
                        });

                        subtotalDetalle += extra.PrecioAdicional * detalle.Cantidad;
                    }
                }

                subtotal += subtotalDetalle;
                pedido.Detalles.Add(detalle);
            }

            pedido.Total = subtotal + costoEnvio;
            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group(admin.Id.ToString())
                .SendAsync("NuevoPedido", new
                {
                    pedidoId = pedido.Id,
                    codigoSeguimiento = $"PED-{pedido.Id:D6}",
                    nombreCliente = pedido.NombreCliente,
                    total = pedido.Total,
                    fechaCreacion = pedido.Fecha
                });

            // Recargamos con Includes para tener nombres reales (local/productos/extras/variantes) sin ciclos.
            var pedidoRecargado = await _context.Pedidos
                .AsNoTracking()
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
                .FirstOrDefaultAsync(p => p.Id == pedido.Id);

            var codigoSeguimiento = $"PED-{pedido.Id:D6}";

            var resumen = GenerarResumenWhatsApp(
                pedidoRecargado ?? pedido,
                admin.NombreLocal,
                codigoSeguimiento);

            return new PedidoCreateResponseDTO
            {
                PedidoId = pedido.Id,
                CodigoSeguimiento = codigoSeguimiento,
                Estado = (pedidoRecargado ?? pedido).Estado,
                Subtotal = subtotal,
                CostoEnvio = costoEnvio,
                Total = (pedidoRecargado ?? pedido).Total,
                Mensaje = "Pedido creado correctamente",
                ResumenWhatsApp = resumen
            };
        }

        public async Task<string?> ObtenerResumenWhatsAppAdmin(int pedidoId, int adminId)
        {
            var pedido = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Administrador)
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
                .FirstOrDefaultAsync(p => p.Id == pedidoId && p.AdministradorId == adminId);

            if (pedido == null)
                return null;

            var nombreLocal = pedido.Administrador?.NombreLocal ?? "Local";
            var codigoSeguimiento = $"PED-{pedido.Id:D6}";
            return GenerarResumenWhatsApp(pedido, nombreLocal, codigoSeguimiento);
        }

        public async Task<IEnumerable<Pedido>> ObtenerFiltrados(int adminId, string? estado, DateTime? desde, DateTime? hasta, string? formaPago, string? formaEntrega)
        {
            return await _pedidoRepository.ObtenerFiltrados(adminId, estado, desde, hasta, formaPago, formaEntrega);
        }

        public async Task<IEnumerable<ComentarioPedido>?> GetComentariosAsync(int pedidoId, int adminId)
        {
            return await _pedidoRepository.GetComentariosAsync(pedidoId, adminId);
        }

        public async Task<ComentarioPedido?> AddComentarioAsync(int pedidoId, int adminId, string texto)
        {
            var pedidoExiste = await _context.Pedidos
                .AnyAsync(p => p.Id == pedidoId && p.AdministradorId == adminId);

            if (!pedidoExiste)
                return null;

            var comentario = new ComentarioPedido
            {
                PedidoId = pedidoId,
                AdministradorId = adminId,
                Texto = texto,
                FechaCreacion = DateTime.UtcNow
            };

            await _pedidoRepository.AddComentarioAsync(comentario);
            return comentario;
        }

        public async Task<ComandaResponseDTO?> GetComandaAsync(int pedidoId, int adminId)
        {
            var pedido = await _pedidoRepository.GetComandaAsync(pedidoId, adminId);
            if (pedido == null)
                return null;

            return new ComandaResponseDTO
            {
                NumeroPedido = pedido.Id,
                CodigoSeguimiento = $"PED-{pedido.Id:D6}",
                FechaCreacion = pedido.Fecha,
                FormaEntrega = pedido.FormaEntrega,
                NombreCliente = pedido.NombreCliente ?? string.Empty,
                DireccionCliente = pedido.DireccionCliente,
                ReferenciaDireccion = pedido.ReferenciaDireccion,
                Items = pedido.Detalles.Select(d => new ComandaItemDTO
                {
                    NombreProducto = d.Producto?.Nombre ?? $"Producto #{d.ProductoId}",
                    Cantidad = d.Cantidad,
                    Extras = d.ProductosExtra
                        .Select(e => e.ProductoExtra?.Nombre)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n!)
                        .ToList()
                }).ToList()
            };
        }

        public async Task<TicketResponseDTO?> GetTicketAsync(int pedidoId, int adminId)
        {
            var pedido = await _pedidoRepository.GetTicketAsync(pedidoId, adminId);
            if (pedido == null)
                return null;

            var items = pedido.Detalles.Select(d =>
            {
                var extrasSubtotal = d.ProductosExtra
                    .Sum(e => e.ProductoExtra?.PrecioAdicional ?? 0m);

                return new TicketItemDTO
                {
                    NombreProducto = d.Producto?.Nombre ?? $"Producto #{d.ProductoId}",
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = (d.PrecioUnitario + extrasSubtotal) * d.Cantidad,
                    Extras = d.ProductosExtra.Select(e => new TicketExtraDTO
                    {
                        Nombre = e.ProductoExtra?.Nombre ?? string.Empty,
                        PrecioAdicional = e.ProductoExtra?.PrecioAdicional ?? 0m
                    }).ToList()
                };
            }).ToList();

            var subtotal = items.Sum(i => i.Subtotal);
            var costoEnvio = pedido.Total - subtotal;

            decimal? vuelto = null;
            if (pedido.FormaPago == "Efectivo"
                && pedido.MontoPagoEfectivo.HasValue
                && pedido.MontoPagoEfectivo.Value > pedido.Total)
            {
                vuelto = pedido.MontoPagoEfectivo.Value - pedido.Total;
            }

            return new TicketResponseDTO
            {
                NumeroPedido = pedido.Id,
                CodigoSeguimiento = $"PED-{pedido.Id:D6}",
                NombreLocal = pedido.Administrador?.NombreLocal ?? string.Empty,
                TelefonoLocal = pedido.Administrador?.Telefono ?? string.Empty,
                FechaCreacion = pedido.Fecha,
                NombreCliente = pedido.NombreCliente ?? string.Empty,
                TelefonoCliente = pedido.TelefonoCliente ?? string.Empty,
                FormaEntrega = pedido.FormaEntrega,
                DireccionCliente = pedido.DireccionCliente,
                ReferenciaDireccion = pedido.ReferenciaDireccion,
                FormaPago = pedido.FormaPago,
                Items = items,
                Subtotal = subtotal,
                CostoEnvio = costoEnvio,
                Total = pedido.Total,
                MontoPagoEfectivo = pedido.MontoPagoEfectivo,
                Vuelto = vuelto
            };
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().ToLowerInvariant();
            normalized = normalized.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");
            var parts = normalized.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join("-", parts);
        }

        private static string GenerarResumenWhatsApp(Pedido pedido, string nombreLocal, string codigoSeguimiento)
        {
            var sb = new StringBuilder();

            sb.AppendLine("📦 *Nuevo pedido recibido:*\n");
            sb.AppendLine($"🏪 *Local:* {nombreLocal}");
            sb.AppendLine($"🧾 *PedidoId:* {pedido.Id} | *Código:* {codigoSeguimiento}");
            sb.AppendLine($"📅 *Fecha:* {pedido.Fecha:yyyy-MM-dd HH:mm}\n");
            sb.AppendLine($"🧑 *Cliente:* {pedido.NombreCliente}");
            sb.AppendLine($"📞 *Teléfono:* {pedido.TelefonoCliente}\n");
            sb.AppendLine("🛍️ *Productos:*");

            foreach (var detalle in pedido.Detalles)
            {
                var nombreProducto = detalle.Producto?.Nombre ?? $"Producto #{detalle.ProductoId}";

                var descripcionVariante = string.Empty;
                if (detalle.VarianteProducto != null)
                {
                    var v = detalle.VarianteProducto;
                    descripcionVariante = v.Opcion2 != null
                        ? $" ({v.Opcion1.Valor} / {v.Opcion2.Valor})"
                        : $" ({v.Opcion1.Valor})";
                }

                sb.AppendLine($"- {detalle.Cantidad} x {nombreProducto}{descripcionVariante} (${detalle.PrecioUnitario} c/u)");

                var extras = detalle.ProductosExtra?
                    .Select(e => e.ProductoExtra?.Nombre)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                if (extras != null && extras.Count > 0)
                    sb.AppendLine($"   ➕ Extras: {string.Join(", ", extras)}");
            }

            sb.AppendLine($"\n💰 *Total:* ${pedido.Total}");
            sb.AppendLine($"\n💳 *Pago:* {pedido.FormaPago}");

            if (pedido.FormaPago == "Transferencia")
            {
                // El modelo actual no tiene datos bancarios; dejamos placeholders para completarlos luego si existen.
                sb.AppendLine("🏦 *Transferencia:* Alias: --- | Titular: ---");
            }

            if (pedido.FormaPago == "Efectivo" && pedido.MontoPagoEfectivo.HasValue)
            {
                var vuelto = pedido.MontoPagoEfectivo.Value - pedido.Total;
                sb.AppendLine($"💵 *Paga con:* ${pedido.MontoPagoEfectivo.Value} (vuelto ${vuelto})");
            }

            sb.AppendLine($"\n📍 *Entrega:* {pedido.FormaEntrega}");

            if (pedido.FormaEntrega == "Delivery")
            {
                sb.AppendLine($"🏠 *Dirección:* {pedido.DireccionCliente}");

                if (!string.IsNullOrWhiteSpace(pedido.ReferenciaDireccion))
                    sb.AppendLine($"🧭 *Referencia:* {pedido.ReferenciaDireccion}");

                if (!string.IsNullOrWhiteSpace(pedido.UbicacionUrl))
                    sb.AppendLine($"📌 *Ubicación:* {pedido.UbicacionUrl}");
            }

            return sb.ToString();
        }






    }
}
