using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Helpers;
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
        private readonly IStockService _stockService;
        private readonly IDescuentoCalculatorService _calculatorService;
        private readonly ILogger<PedidoService> _logger;

        public PedidoService(
            IPedidoRepository pedidoRepository,
            AppDbContext context,
            IHubContext<PedidosHub> hubContext,
            IStockService stockService,
            IDescuentoCalculatorService calculatorService,
            ILogger<PedidoService> logger)
        {
            _pedidoRepository = pedidoRepository;
            _context = context;
            _hubContext = hubContext;
            _stockService = stockService;
            _calculatorService = calculatorService;
            _logger = logger;
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

            // EF no puede traducir Slugify() a SQL, traemos admins activos y filtramos en memoria.
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

            decimal costoEnvio = request.FormaEntrega == "Delivery" && admin.CostoEnvio.HasValue
                ? admin.CostoEnvio.Value
                : 0m;

            // --- Primera pasada: validar y recolectar datos para el cálculo ---
            var lineasParaCalculo = new List<LineaParaCalculo>();
            var detallesInfo = new List<(PedidoDetalleCreateDTO dto, int productoId, int? varianteId, decimal precioUnit, List<int> extrasIds, decimal extrasSubtotal)>();
            decimal subtotalExtrasTotal = 0m;

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

                var extrasIds = new List<int>();
                decimal extrasSubtotal = 0m;

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

                        extrasIds.Add(extraId);
                        extrasSubtotal += extra.PrecioAdicional * detalleDTO.Cantidad;
                    }
                }

                subtotalExtrasTotal += extrasSubtotal;
                lineasParaCalculo.Add(new LineaParaCalculo
                {
                    ProductoId = producto.Id,
                    CategoriaId = producto.CategoriaId,
                    PrecioUnitario = precioUnitario,
                    Cantidad = detalleDTO.Cantidad
                });
                detallesInfo.Add((detalleDTO, producto.Id, varianteProductoId, precioUnitario, extrasIds, extrasSubtotal));
            }

            // --- Cargar y filtrar descuentos activos ---
            var ahora = DateTime.UtcNow;
            var todosDescuentos = await _context.Descuentos
                .Where(d => d.AdministradorId == admin.Id)
                .ToListAsync();

            var descuentosActivos = todosDescuentos.Where(d =>
                d.Activo
                && (d.FechaInicio == null || d.FechaInicio <= ahora)
                && (d.FechaFin == null || d.FechaFin >= ahora)
            ).ToList();

            // --- Calcular descuentos de productos/categorías/pedido completo ---
            var resultado = _calculatorService.CalcularDescuentos(lineasParaCalculo, descuentosActivos);

            // --- Validar y calcular cupón ---
            decimal montoDescuentoCupon = 0m;
            Cupon? cuponUsado = null;
            string? codigoCuponNorm = null;

            if (!string.IsNullOrWhiteSpace(request.CodigoCupon))
            {
                codigoCuponNorm = request.CodigoCupon.Trim().ToUpperInvariant();

                cuponUsado = await _context.Cupones
                    .FirstOrDefaultAsync(c => c.Codigo == codigoCuponNorm && c.AdministradorId == admin.Id);

                if (cuponUsado == null || !cuponUsado.Activo)
                    throw new ValidacionException("El cupón no es válido o no está activo");

                if (cuponUsado.FechaVencimiento.HasValue && cuponUsado.FechaVencimiento.Value < ahora)
                    throw new ValidacionException("El cupón ha vencido");

                if (cuponUsado.LimiteUsos.HasValue && cuponUsado.UsosActuales >= cuponUsado.LimiteUsos.Value)
                    throw new ValidacionException("El cupón ha alcanzado su límite de usos");

                var subtotalParaCupon = resultado.SubtotalFinal;
                if (cuponUsado.PedidoMinimo.HasValue && subtotalParaCupon < cuponUsado.PedidoMinimo.Value)
                    throw new ValidacionException($"El pedido mínimo para este cupón es ${cuponUsado.PedidoMinimo.Value:N2}");

                montoDescuentoCupon = cuponUsado.Tipo == "Porcentaje"
                    ? Math.Round(subtotalParaCupon * cuponUsado.Valor / 100, 2, MidpointRounding.AwayFromZero)
                    : cuponUsado.Valor;

                montoDescuentoCupon = Math.Min(montoDescuentoCupon, subtotalParaCupon);
            }

            // --- Construir el pedido con totales calculados ---
            var totalFinal = resultado.SubtotalFinal - montoDescuentoCupon + subtotalExtrasTotal + costoEnvio;
            if (totalFinal < 0) totalFinal = 0m;

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
                SubtotalSinDescuentos = resultado.SubtotalSinDescuentos,
                MontoDescuentoProductos = resultado.MontoDescuentoProductos + resultado.MontoDescuentoPedidoCompleto,
                MontoDescuentoCupon = montoDescuentoCupon,
                CuponId = cuponUsado?.Id,
                CodigoCupon = codigoCuponNorm,
                Total = totalFinal,
                CodigoSeguimiento = GenerarCodigoSeguimiento(),
                Detalles = new List<DetallePedido>()
            };

            // --- Segunda pasada: construir DetallePedido ---
            var detalleEntidades = new List<DetallePedido>();
            for (int i = 0; i < detallesInfo.Count; i++)
            {
                var (dto, productoId, varianteId, precioUnit, extrasIds, _) = detallesInfo[i];

                var detalle = new DetallePedido
                {
                    ProductoId = productoId,
                    Cantidad = dto.Cantidad,
                    PrecioUnitario = resultado.Lineas[i].PrecioUnitarioConDescuento,
                    VarianteProductoId = varianteId,
                    ProductosExtra = extrasIds.Select(eid => new DetallePedidoExtra { ProductoExtraId = eid }).ToList()
                };

                pedido.Detalles.Add(detalle);
                detalleEntidades.Add(detalle);
            }

            _context.Pedidos.Add(pedido);

            // --- Registros de auditoría de descuentos por línea ---
            for (int i = 0; i < detalleEntidades.Count; i++)
            {
                foreach (var da in resultado.Lineas[i].DescuentosAplicados)
                {
                    _context.DetallePedidoDescuentos.Add(new DetallePedidoDescuento
                    {
                        Pedido = pedido,
                        DetallePedido = detalleEntidades[i],
                        DescuentoId = da.DescuentoId,
                        NombreDescuentoSnapshot = da.NombreDescuento,
                        TipoDescuento = da.TipoAlcance,
                        MontoDescontado = da.MontoDescontado
                    });
                }
            }

            // --- Registros de auditoría de descuentos globales ---
            foreach (var dg in resultado.DescuentosPedidoCompletoAplicados)
            {
                _context.DetallePedidoDescuentos.Add(new DetallePedidoDescuento
                {
                    Pedido = pedido,
                    DescuentoId = dg.DescuentoId,
                    NombreDescuentoSnapshot = dg.NombreDescuento,
                    TipoDescuento = "PedidoCompleto",
                    MontoDescontado = dg.MontoDescontado
                });
            }

            // --- UsoCupon y actualizar contador ---
            if (cuponUsado != null)
            {
                _context.UsosCupones.Add(new UsoCupon
                {
                    Cupon = cuponUsado,
                    Pedido = pedido,
                    MontoDescontado = montoDescuentoCupon
                });
                cuponUsado.UsosActuales++;
            }

            // --- Guardar todo en una sola operación ---
            await _context.SaveChangesAsync();

            await _hubContext.Clients
                .Group(admin.Id.ToString())
                .SendAsync("NuevoPedido", new
                {
                    pedidoId = pedido.Id,
                    codigoSeguimiento = pedido.CodigoSeguimiento,
                    nombreCliente = pedido.NombreCliente,
                    total = pedido.Total,
                    fechaCreacion = pedido.Fecha
                });

            // Recargamos con Includes para tener nombres reales sin ciclos.
            var pedidoRecargado = await _context.Pedidos
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
                .FirstOrDefaultAsync(p => p.Id == pedido.Id);

            var numeroVisible = $"PED-{pedido.Id:D6}";
            var codigoSeguimiento = pedido.CodigoSeguimiento!;

            var resumen = GenerarResumenWhatsApp(
                pedidoRecargado ?? pedido,
                admin.NombreLocal,
                numeroVisible);

            return new PedidoCreateResponseDTO
            {
                PedidoId = pedido.Id,
                CodigoSeguimiento = codigoSeguimiento,
                Estado = (pedidoRecargado ?? pedido).Estado,
                Subtotal = resultado.SubtotalSinDescuentos + subtotalExtrasTotal,
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

        public async Task<EstadoPagoPublicoResponseDTO> ObtenerEstadoPagoPublico(string codigoSeguimiento)
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
                .FirstOrDefaultAsync(p => p.CodigoSeguimiento == codigoSeguimiento);

            if (pedido == null)
                return new EstadoPagoPublicoResponseDTO { Encontrado = false };

            var nombreLocal = pedido.Administrador?.NombreLocal ?? "Local";
            var numeroVisible = $"PED-{pedido.Id:D6}";
            var resumen = GenerarResumenWhatsApp(pedido, nombreLocal, numeroVisible);

            return new EstadoPagoPublicoResponseDTO
            {
                Encontrado = true,
                Estado = pedido.Estado,
                MercadoPagoStatus = pedido.MercadoPagoStatus,
                Total = pedido.Total,
                ResumenWhatsApp = resumen,
                NombreCliente = pedido.NombreCliente,
                LinkWhatsapp = pedido.Administrador?.LinkWhatsapp
            };
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
            var costoEnvio = pedido.Total - subtotal + pedido.MontoDescuentoCupon;

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
                SubtotalSinDescuentos = pedido.SubtotalSinDescuentos,
                MontoDescuentoProductos = pedido.MontoDescuentoProductos,
                MontoDescuentoCupon = pedido.MontoDescuentoCupon,
                CodigoCupon = pedido.CodigoCupon,
                Subtotal = subtotal,
                CostoEnvio = costoEnvio,
                Total = pedido.Total,
                MontoPagoEfectivo = pedido.MontoPagoEfectivo,
                Vuelto = vuelto
            };
        }

        public async Task<(bool encontrado, string? error, List<string> advertencias)> CambiarEstado(int pedidoId, string nuevoEstado, int adminId)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                .FirstOrDefaultAsync(p => p.Id == pedidoId && p.AdministradorId == adminId);

            if (pedido == null)
                return (false, null, new List<string>());

            var estadoAnterior = pedido.Estado;
            var codigo = $"PED-{pedido.Id:D6}";
            var ahora = DateTime.UtcNow;

            if (estadoAnterior == "Entregado")
                return (true, "Un pedido entregado no puede cambiar de estado.", new List<string>());

            if (nuevoEstado == "Confirmado")
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Re-aplicar cupón antes de descontar stock (modifica pedido en memoria)
                    var advertenciasConfirmado = new List<string>();
                    if (estadoAnterior == "Cancelado" && pedido.CuponId.HasValue)
                        advertenciasConfirmado.AddRange(await ReaplicarCuponAsync(pedido, ahora));

                    foreach (var detalle in pedido.Detalles)
                    {
                        await _stockService.DescontarStock(
                            detalle.ProductoId,
                            detalle.VarianteProductoId,
                            detalle.Cantidad,
                            $"Pedido #{codigo}",
                            adminId);
                    }

                    pedido.Estado = nuevoEstado;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return (true, null, advertenciasConfirmado);
                }
                catch (InvalidOperationException ex)
                {
                    await transaction.RollbackAsync();
                    return (true, ex.Message, new List<string>());
                }
            }
            else if (nuevoEstado == "Cancelado")
            {
                if (estadoAnterior == "Confirmado")
                {
                    foreach (var detalle in pedido.Detalles)
                    {
                        try
                        {
                            await _stockService.ReponerStock(
                                detalle.ProductoId,
                                detalle.VarianteProductoId,
                                detalle.Cantidad,
                                $"Cancelación pedido #{codigo}",
                                adminId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al reponer stock para detalle {DetalleId} del pedido {PedidoId}", detalle.Id, pedidoId);
                        }
                    }
                }

                if (pedido.CuponId.HasValue)
                    await LiberarCuponAsync(pedido, ahora);

                pedido.Estado = nuevoEstado;
                await _context.SaveChangesAsync();
                return (true, null, new List<string>());
            }
            else
            {
                // Cualquier otro estado desde Cancelado: re-aplicar cupón si corresponde
                var advertencias = new List<string>();
                if (estadoAnterior == "Cancelado" && pedido.CuponId.HasValue)
                    advertencias.AddRange(await ReaplicarCuponAsync(pedido, ahora));

                pedido.Estado = nuevoEstado;
                await _context.SaveChangesAsync();
                return (true, null, advertencias);
            }
        }

        private async Task LiberarCuponAsync(Pedido pedido, DateTime ahora)
        {
            var usoCupon = await _context.UsosCupones
                .FirstOrDefaultAsync(u => u.PedidoId == pedido.Id && !u.Liberado);

            if (usoCupon != null)
            {
                usoCupon.Liberado = true;
                usoCupon.FechaLiberacion = ahora;
            }

            var cupon = await _context.Cupones.FindAsync(pedido.CuponId);
            if (cupon != null && cupon.UsosActuales > 0)
                cupon.UsosActuales--;
        }

        private async Task<List<string>> ReaplicarCuponAsync(Pedido pedido, DateTime ahora)
        {
            var cupon = await _context.Cupones.FindAsync(pedido.CuponId);

            bool disponible = cupon != null
                && cupon.Activo
                && (cupon.FechaVencimiento == null || cupon.FechaVencimiento >= ahora)
                && (cupon.LimiteUsos == null || cupon.UsosActuales < cupon.LimiteUsos);

            if (!disponible)
            {
                var codigoSnap = pedido.CodigoCupon ?? "desconocido";
                var totalRecalculado = pedido.Total + pedido.MontoDescuentoCupon;
                pedido.Total = totalRecalculado;
                pedido.MontoDescuentoCupon = 0m;
                pedido.CuponId = null;

                return new List<string>
                {
                    $"El cupón {codigoSnap} ya no está disponible. Se reactivó el pedido sin descuento del cupón. Total recalculado: ${totalRecalculado:N2}"
                };
            }

            var usoCupon = await _context.UsosCupones
                .FirstOrDefaultAsync(u => u.PedidoId == pedido.Id && u.Liberado);

            if (usoCupon != null)
            {
                usoCupon.Liberado = false;
                usoCupon.FechaLiberacion = null;
                cupon!.UsosActuales++;
            }

            return new List<string>();
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

        private static string GenerarCodigoSeguimiento()
        {
            var guid = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            return $"{guid.Substring(0, 4)}-{guid.Substring(4, 4)}-{guid.Substring(8, 4)}";
        }

        private static string GenerarResumenWhatsApp(Pedido pedido, string nombreLocal, string codigoSeguimiento)
        {
            var culture = new System.Globalization.CultureInfo("es-AR");
            string Fmt(decimal m) => m.ToString("N0", culture);
            const string Separator = "────────────────────";

            var sb = new StringBuilder();

            sb.AppendLine("¡Nuevo pedido! 🎉");
            sb.AppendLine($"#{codigoSeguimiento} · {nombreLocal}");
            sb.AppendLine($"{pedido.Fecha:dd/MM/yy} - {pedido.Fecha:HH:mm} hs");
            sb.AppendLine("Cliente");
            sb.AppendLine(pedido.NombreCliente);
            sb.AppendLine(pedido.TelefonoCliente);
            sb.AppendLine("Productos");

            // totalExtras = suma de PrecioAdicional × Cantidad para cada extra de cada detalle
            var totalExtras = pedido.Detalles.Sum(d =>
                (d.ProductosExtra ?? Enumerable.Empty<DetallePedidoExtra>())
                .Sum(e => (e.ProductoExtra?.PrecioAdicional ?? 0m) * d.Cantidad));

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

                sb.AppendLine($"{detalle.Cantidad}x {nombreProducto}{descripcionVariante}: ${Fmt(detalle.PrecioUnitario)}");

                foreach (var extra in detalle.ProductosExtra ?? Enumerable.Empty<DetallePedidoExtra>())
                {
                    var nombre = extra.ProductoExtra?.Nombre;
                    if (string.IsNullOrWhiteSpace(nombre)) continue;
                    var precio = extra.ProductoExtra?.PrecioAdicional ?? 0m;
                    sb.AppendLine(precio > 0 ? $"  + {nombre}: ${Fmt(precio)}" : $"  + {nombre}");
                }
            }

            bool hayDescuentos = pedido.MontoDescuentoProductos > 0 || pedido.MontoDescuentoCupon > 0;

            // subtotalBruto = base de productos + extras, sin ningún descuento aplicado
            var subtotalBruto = pedido.SubtotalSinDescuentos + totalExtras;

            if (hayDescuentos)
            {
                sb.AppendLine($"Subtotal bruto: ${Fmt(subtotalBruto)}");
                if (pedido.MontoDescuentoProductos > 0)
                    sb.AppendLine($"Descuentos aplicados: -${Fmt(pedido.MontoDescuentoProductos)}");
                if (pedido.MontoDescuentoCupon > 0)
                    sb.AppendLine($"Cupón {pedido.CodigoCupon}: -${Fmt(pedido.MontoDescuentoCupon)}");
            }

            var netSubtotal = subtotalBruto - pedido.MontoDescuentoProductos - pedido.MontoDescuentoCupon;
            var costoEnvio = pedido.Total - netSubtotal;

            sb.AppendLine(Separator);
            sb.AppendLine($"Subtotal: ${Fmt(netSubtotal)}");
            if (costoEnvio > 0)
                sb.AppendLine($"Envío: ${Fmt(costoEnvio)}");
            sb.AppendLine(Separator);
            sb.AppendLine($"Total: ${Fmt(pedido.Total)}");
            sb.AppendLine($"Pago: {pedido.FormaPago}");

            if (pedido.FormaPago == "Transferencia")
            {
                var admin = pedido.Administrador;
                if (!string.IsNullOrWhiteSpace(admin?.AliasTransferencia))
                    sb.AppendLine($"Alias: {admin.AliasTransferencia}");
                if (!string.IsNullOrWhiteSpace(admin?.TitularCuenta))
                    sb.AppendLine($"Titular: {admin.TitularCuenta}");
            }

            if (pedido.FormaPago == "Efectivo" && pedido.MontoPagoEfectivo.HasValue)
            {
                var vuelto = pedido.MontoPagoEfectivo.Value - pedido.Total;
                sb.AppendLine($"Paga con: ${Fmt(pedido.MontoPagoEfectivo.Value)} (vuelto ${Fmt(vuelto)})");
            }

            var textoEntrega = pedido.FormaEntrega == "Delivery" ? "Delivery" : "Retira en el local";
            sb.AppendLine($"Entrega: {textoEntrega}");

            if (pedido.FormaEntrega == "Delivery")
            {
                if (!string.IsNullOrWhiteSpace(pedido.DireccionCliente))
                    sb.AppendLine($"Dirección: {pedido.DireccionCliente}");
                if (!string.IsNullOrWhiteSpace(pedido.ReferenciaDireccion))
                    sb.AppendLine($"Referencia: {pedido.ReferenciaDireccion}");
                if (!string.IsNullOrWhiteSpace(pedido.UbicacionUrl))
                    sb.AppendLine($"Ubicación: {pedido.UbicacionUrl}");
            }

            sb.Append("¡Espero tu confirmación!");

            return sb.ToString();
        }






    }
}
