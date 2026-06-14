using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class ReporteService : IReporteService
    {
        private readonly AppDbContext _context;

        private static readonly TimeZoneInfo ArgentinaTZ = ResolveArgentinaTimeZone();

        public ReporteService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardReporteDTO> ObtenerDashboardAsync(int adminId, string periodo)
        {
            var rangos = CalcularRangos(periodo);

            var desdeUtc = ToUtc(rangos.Desde);
            var hastaUtc = ToUtc(rangos.Hasta);
            var desdeCmpUtc = ToUtc(rangos.DesdeCmp);
            var hastaCmpUtc = ToUtc(rangos.HastaCmp);

            // Pedidos del período actual con todos sus detalles y productos
            var pedidosActuales = await _context.Pedidos
                .AsNoTracking()
                .Where(p => p.AdministradorId == adminId
                         && p.Estado != "Cancelado"
                         && p.Fecha >= desdeUtc
                         && p.Fecha < hastaUtc)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto!)
                        .ThenInclude(prod => prod.Categoria)
                .ToListAsync();

            // Para el período de comparación solo necesitamos los totales
            var pedidosComparacion = await _context.Pedidos
                .AsNoTracking()
                .Where(p => p.AdministradorId == adminId
                         && p.Estado != "Cancelado"
                         && p.Fecha >= desdeCmpUtc
                         && p.Fecha < hastaCmpUtc)
                .Select(p => new { p.Total })
                .ToListAsync();

            var ventas = CalcularVentas(
                pedidosActuales.Select(p => p.Total).ToList(),
                pedidosActuales.Count,
                pedidosComparacion.Select(p => p.Total).ToList(),
                pedidosComparacion.Count);

            return new DashboardReporteDTO
            {
                Periodo = new RangoFechasDTO { Desde = rangos.Desde, Hasta = rangos.Hasta, Label = rangos.Label },
                Comparacion = new RangoFechasDTO { Desde = rangos.DesdeCmp, Hasta = rangos.HastaCmp, Label = rangos.LabelCmp },
                Ventas = ventas,
                SerieVentas = CalcularSerie(pedidosActuales, periodo, rangos.Desde, rangos.Hasta),
                TopProductos = CalcularTopProductos(pedidosActuales),
                TopCategorias = CalcularTopCategorias(pedidosActuales),
                TopClientes = CalcularTopClientes(pedidosActuales),
                MetodosPago = CalcularMetodosPago(pedidosActuales),
                HorasPico = CalcularHorasPico(pedidosActuales),
                DiasPico = CalcularDiasPico(pedidosActuales),
            };
        }

        // ── Rangos de fecha ───────────────────────────────────────────────

        private record Rangos(DateTime Desde, DateTime Hasta, string Label,
                              DateTime DesdeCmp, DateTime HastaCmp, string LabelCmp);

        private static Rangos CalcularRangos(string periodo)
        {
            var ahora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ArgentinaTZ);
            var hoy = new DateTime(ahora.Year, ahora.Month, ahora.Day, 0, 0, 0, DateTimeKind.Unspecified);

            switch (periodo?.ToLowerInvariant())
            {
                case "hoy":
                {
                    var desde = hoy;
                    var hasta = hoy.AddDays(1);
                    return new Rangos(desde, hasta, "Hoy",
                                      hoy.AddDays(-1), hoy, "Ayer");
                }
                case "semana":
                {
                    // DayOfWeek: Sun=0, Mon=1, ..., Sat=6  → días desde lunes
                    var diasDesdeLunes = ((int)hoy.DayOfWeek + 6) % 7;
                    var lunes = hoy.AddDays(-diasDesdeLunes);
                    var hasta = hoy.AddDays(1);
                    var dias = (hasta - lunes).TotalDays;
                    var desdeCmp = lunes.AddDays(-7);
                    var hastaCmp = desdeCmp.AddDays(dias);
                    return new Rangos(lunes, hasta, "Esta semana",
                                      desdeCmp, hastaCmp, "Semana pasada");
                }
                case "mes":
                {
                    var desde = new DateTime(hoy.Year, hoy.Month, 1);
                    var hasta = hoy.AddDays(1);
                    var dias = (hasta - desde).TotalDays;
                    var desdeCmp = desde.AddMonths(-1);
                    var hastaCmp = desdeCmp.AddDays(dias);
                    return new Rangos(desde, hasta, "Este mes",
                                      desdeCmp, hastaCmp, "Mes pasado");
                }
                case "anio":
                case "ano":
                case "año":
                {
                    var desde = new DateTime(hoy.Year, 1, 1);
                    var hasta = hoy.AddDays(1);
                    var dias = (hasta - desde).TotalDays;
                    var desdeCmp = new DateTime(hoy.Year - 1, 1, 1);
                    var hastaCmp = desdeCmp.AddDays(dias);
                    return new Rangos(desde, hasta, "Este año",
                                      desdeCmp, hastaCmp, "Año pasado");
                }
                default:
                    throw new ArgumentException(
                        $"Período inválido: '{periodo}'. Valores aceptados: hoy, semana, mes, anio.");
            }
        }

        private static DateTime ToUtc(DateTime fechaArgentina)
        {
            var argSpecified = DateTime.SpecifyKind(fechaArgentina, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(argSpecified, ArgentinaTZ);
        }

        private static DateTime ToArgentina(DateTime fechaUtc)
        {
            var utcSpecified = fechaUtc.Kind == DateTimeKind.Utc
                ? fechaUtc
                : DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utcSpecified, ArgentinaTZ);
        }

        // ── Ventas (KPIs) ─────────────────────────────────────────────────

        private static VentasResumenDTO CalcularVentas(
            List<decimal> totalesAct, int cantAct,
            List<decimal> totalesCmp, int cantCmp)
        {
            var totalAct = totalesAct.Sum();
            var totalCmp = totalesCmp.Sum();
            var ticketAct = cantAct > 0 ? totalAct / cantAct : 0m;
            var ticketCmp = cantCmp > 0 ? totalCmp / cantCmp : 0m;

            return new VentasResumenDTO
            {
                Total = totalAct,
                TotalAnterior = totalCmp,
                VariacionPorcentual = VariacionDecimal(totalAct, totalCmp),
                CantidadPedidos = cantAct,
                CantidadPedidosAnterior = cantCmp,
                VariacionPedidos = VariacionDecimal(cantAct, cantCmp),
                TicketPromedio = Math.Round(ticketAct, 2),
                TicketPromedioAnterior = Math.Round(ticketCmp, 2),
                VariacionTicket = VariacionDecimal(ticketAct, ticketCmp)
            };
        }

        private static decimal? VariacionDecimal(decimal actual, decimal anterior)
        {
            if (anterior == 0m) return null;
            return Math.Round(((actual - anterior) / anterior) * 100m, 1);
        }

        // ── Serie temporal ────────────────────────────────────────────────

        private static List<PuntoSerieDTO> CalcularSerie(
            List<Pedido> pedidos, string periodo, DateTime desdeArg, DateTime hastaArg)
        {
            var puntos = new List<PuntoSerieDTO>();
            var pedidosArg = pedidos.Select(p => new { FechaArg = ToArgentina(p.Fecha), p.Total }).ToList();

            switch (periodo.ToLowerInvariant())
            {
                case "hoy":
                    for (int h = 0; h < 24; h++)
                    {
                        var lote = pedidosArg.Where(p => p.FechaArg.Hour == h).ToList();
                        puntos.Add(new PuntoSerieDTO
                        {
                            Etiqueta = $"{h:D2}h",
                            Total = lote.Sum(p => p.Total),
                            Cantidad = lote.Count
                        });
                    }
                    break;

                case "semana":
                case "mes":
                {
                    var dia = desdeArg;
                    while (dia < hastaArg)
                    {
                        var lote = pedidosArg.Where(p => p.FechaArg.Date == dia.Date).ToList();
                        puntos.Add(new PuntoSerieDTO
                        {
                            Etiqueta = dia.ToString("dd/MM"),
                            Total = lote.Sum(p => p.Total),
                            Cantidad = lote.Count
                        });
                        dia = dia.AddDays(1);
                    }
                    break;
                }

                default: // anio
                {
                    var mes = new DateTime(desdeArg.Year, desdeArg.Month, 1);
                    while (mes < hastaArg)
                    {
                        var lote = pedidosArg.Where(p =>
                            p.FechaArg.Year == mes.Year && p.FechaArg.Month == mes.Month).ToList();
                        puntos.Add(new PuntoSerieDTO
                        {
                            Etiqueta = mes.ToString("MMM").TrimEnd('.'),
                            Total = lote.Sum(p => p.Total),
                            Cantidad = lote.Count
                        });
                        mes = mes.AddMonths(1);
                    }
                    break;
                }
            }

            return puntos;
        }

        // ── Top productos / categorías / clientes ────────────────────────

        private static List<TopProductoDTO> CalcularTopProductos(List<Pedido> pedidos)
        {
            return pedidos.SelectMany(p => p.Detalles)
                .Where(d => d.Producto != null)
                .GroupBy(d => new { d.ProductoId, Nombre = d.Producto!.Nombre })
                .Select(g => new TopProductoDTO
                {
                    ProductoId = g.Key.ProductoId,
                    Nombre = g.Key.Nombre,
                    Unidades = g.Sum(d => d.Cantidad),
                    Facturacion = g.Sum(d => d.Cantidad * d.PrecioUnitario)
                })
                .OrderByDescending(t => t.Facturacion)
                .Take(10)
                .ToList();
        }

        private static List<TopCategoriaDTO> CalcularTopCategorias(List<Pedido> pedidos)
        {
            return pedidos.SelectMany(p => p.Detalles)
                .Where(d => d.Producto?.Categoria != null)
                .GroupBy(d => new { CategoriaId = d.Producto!.Categoria!.Id, Nombre = d.Producto.Categoria.Nombre })
                .Select(g => new TopCategoriaDTO
                {
                    CategoriaId = g.Key.CategoriaId,
                    Nombre = g.Key.Nombre,
                    Unidades = g.Sum(d => d.Cantidad),
                    Facturacion = g.Sum(d => d.Cantidad * d.PrecioUnitario)
                })
                .OrderByDescending(t => t.Facturacion)
                .Take(10)
                .ToList();
        }

        private static List<TopClienteDTO> CalcularTopClientes(List<Pedido> pedidos)
        {
            return pedidos
                .GroupBy(p => new { p.NombreCliente, p.TelefonoCliente })
                .Select(g => new TopClienteDTO
                {
                    NombreCliente = g.Key.NombreCliente ?? string.Empty,
                    Telefono = g.Key.TelefonoCliente ?? string.Empty,
                    CantidadPedidos = g.Count(),
                    Total = g.Sum(p => p.Total)
                })
                .OrderByDescending(c => c.Total)
                .Take(10)
                .ToList();
        }

        // ── Métodos de pago / horas / días ───────────────────────────────

        private static List<MetodoPagoDTO> CalcularMetodosPago(List<Pedido> pedidos)
        {
            var totalGeneral = pedidos.Sum(p => p.Total);
            return pedidos
                .GroupBy(p => p.FormaPago ?? "(sin dato)")
                .Select(g =>
                {
                    var monto = g.Sum(p => p.Total);
                    return new MetodoPagoDTO
                    {
                        FormaPago = g.Key,
                        Cantidad = g.Count(),
                        Monto = monto,
                        Porcentaje = totalGeneral > 0
                            ? Math.Round((monto / totalGeneral) * 100m, 1)
                            : 0m
                    };
                })
                .OrderByDescending(m => m.Monto)
                .ToList();
        }

        private static List<HoraPicoDTO> CalcularHorasPico(List<Pedido> pedidos)
        {
            var horas = pedidos.Select(p => ToArgentina(p.Fecha).Hour).ToList();
            var resultado = new List<HoraPicoDTO>();
            for (int h = 0; h < 24; h++)
                resultado.Add(new HoraPicoDTO { Hora = h, Cantidad = horas.Count(x => x == h) });
            return resultado;
        }

        private static List<DiaPicoDTO> CalcularDiasPico(List<Pedido> pedidos)
        {
            var dias = pedidos.Select(p => (int)ToArgentina(p.Fecha).DayOfWeek).ToList();
            var resultado = new List<DiaPicoDTO>();
            for (int d = 0; d < 7; d++)
                resultado.Add(new DiaPicoDTO { DiaSemana = d, Cantidad = dias.Count(x => x == d) });
            return resultado;
        }

        // ── TimeZone fallback (Windows / Linux / fallback fijo) ───────────

        private static TimeZoneInfo ResolveArgentinaTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time"); }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone(
                        "AR-Fallback", TimeSpan.FromHours(-3), "Argentina (fallback)", "AR");
                }
            }
        }
    }
}
