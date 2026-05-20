using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    public interface IDescuentoCalculatorService
    {
        /// <summary>
        /// Calcula descuentos para un conjunto de líneas de pedido.
        /// <paramref name="descuentosActivos"/> debe estar ya filtrado por el llamador según los criterios de actividad:
        /// Activo=true, FechaInicio nula o &lt;= UtcNow, FechaFin nula o &gt;= UtcNow.
        /// Este método no accede a base de datos.
        /// </summary>
        CalcularResultado CalcularDescuentos(List<LineaParaCalculo> lineas, List<Descuento> descuentosActivos);
    }
}
