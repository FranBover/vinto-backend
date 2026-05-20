using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class DescuentoCalculatorService : IDescuentoCalculatorService
    {
        public CalcularResultado CalcularDescuentos(List<LineaParaCalculo> lineas, List<Descuento> descuentosActivos)
        {
            var resultado = new CalcularResultado();

            foreach (var linea in lineas)
            {
                var precioUnitario = linea.PrecioUnitario;
                var descuentosAplicados = new List<DescuentoAplicadoLinea>();

                // Descuentos de producto, ordenados por FechaCreacion asc
                var descProducto = descuentosActivos
                    .Where(d => d.ProductoId == linea.ProductoId)
                    .OrderBy(d => d.FechaCreacion);

                foreach (var d in descProducto)
                {
                    var antes = precioUnitario;
                    precioUnitario = Aplicar(precioUnitario, d);
                    descuentosAplicados.Add(new DescuentoAplicadoLinea
                    {
                        DescuentoId = d.Id,
                        NombreDescuento = d.Nombre,
                        TipoAlcance = "Producto",
                        MontoDescontado = Round((antes - precioUnitario) * linea.Cantidad)
                    });
                }

                // Descuentos de categoría sobre el precio ya reducido, ordenados por FechaCreacion asc
                var descCategoria = descuentosActivos
                    .Where(d => d.CategoriaId == linea.CategoriaId)
                    .OrderBy(d => d.FechaCreacion);

                foreach (var d in descCategoria)
                {
                    var antes = precioUnitario;
                    precioUnitario = Aplicar(precioUnitario, d);
                    descuentosAplicados.Add(new DescuentoAplicadoLinea
                    {
                        DescuentoId = d.Id,
                        NombreDescuento = d.Nombre,
                        TipoAlcance = "Categoria",
                        MontoDescontado = Round((antes - precioUnitario) * linea.Cantidad)
                    });
                }

                // Clamp: precio unitario nunca cae por debajo de 0.01
                if (precioUnitario <= 0)
                    precioUnitario = 0.01m;

                resultado.Lineas.Add(new LineaResultado
                {
                    ProductoId = linea.ProductoId,
                    PrecioUnitarioOriginal = linea.PrecioUnitario,
                    PrecioUnitarioConDescuento = precioUnitario,
                    MontoDescontado = Round((linea.PrecioUnitario - precioUnitario) * linea.Cantidad),
                    Cantidad = linea.Cantidad,
                    DescuentosAplicados = descuentosAplicados
                });
            }

            resultado.SubtotalSinDescuentos = lineas.Sum(l => l.PrecioUnitario * l.Cantidad);
            resultado.MontoDescuentoProductos = resultado.Lineas.Sum(l => l.MontoDescontado);

            // Descuentos a pedido completo sobre el subtotal post-items
            var descGlobales = descuentosActivos
                .Where(d => d.AplicaAPedidoCompleto)
                .OrderBy(d => d.FechaCreacion);

            var subtotalConDescItems = resultado.Lineas.Sum(l => l.PrecioUnitarioConDescuento * l.Cantidad);
            var subtotalPostGlobales = subtotalConDescItems;

            foreach (var d in descGlobales)
            {
                var antes = subtotalPostGlobales;
                subtotalPostGlobales = Aplicar(subtotalPostGlobales, d);
                resultado.DescuentosPedidoCompletoAplicados.Add(new DescuentoAplicadoGlobal
                {
                    DescuentoId = d.Id,
                    NombreDescuento = d.Nombre,
                    MontoDescontado = Round(antes - subtotalPostGlobales)
                });
            }

            // Clamp final del subtotal global
            if (subtotalPostGlobales < 0)
                subtotalPostGlobales = 0;

            // MontoDescuentoPedidoCompleto = reducción real (respeta el clamp)
            resultado.MontoDescuentoPedidoCompleto = Round(subtotalConDescItems - subtotalPostGlobales);
            resultado.SubtotalFinal = subtotalPostGlobales;

            return resultado;
        }

        private static decimal Aplicar(decimal precio, Descuento d) =>
            d.Tipo == "Porcentaje"
                ? Round(precio * (1 - d.Valor / 100))
                : Round(precio - d.Valor);

        private static decimal Round(decimal valor) =>
            Math.Round(valor, 2, MidpointRounding.AwayFromZero);
    }
}
