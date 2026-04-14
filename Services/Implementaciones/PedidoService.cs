using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Vinto.Api.Services.Implementaciones
{
    public class PedidoService : IPedidoService
    {
        private readonly AppDbContext _context;
        private readonly IPedidoRepository _pedidoRepository;

        public PedidoService(IPedidoRepository pedidoRepository, AppDbContext context)
        {
            _pedidoRepository = pedidoRepository;
            _context = context;
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

                var detalle = new DetallePedido
                {
                    ProductoId = producto.Id,
                    Cantidad = detalleDTO.Cantidad,
                    PrecioUnitario = producto.Precio,
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
            
            // Recargamos con Includes para tener nombres reales (local/productos/extras) sin ciclos.
            var pedidoRecargado = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.ProductosExtra)
                        .ThenInclude(e => e.ProductoExtra)
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
                .FirstOrDefaultAsync(p => p.Id == pedidoId && p.AdministradorId == adminId);

            if (pedido == null)
                return null;

            var nombreLocal = pedido.Administrador?.NombreLocal ?? "Local";
            var codigoSeguimiento = $"PED-{pedido.Id:D6}";
            return GenerarResumenWhatsApp(pedido, nombreLocal, codigoSeguimiento);
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
                sb.AppendLine($"- {detalle.Cantidad} x {nombreProducto} (${detalle.PrecioUnitario} c/u)");

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
