using Vinto.Api.DTOs;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Helpers
{
    // Arma la respuesta del menú público a partir de la entrada cacheada. Pura: lo que depende del request (host del
    // logo) y de la hora (estado del local) entra por parámetro, así la entrada cacheada nunca se muta ni contiene
    // nada que dependa del momento en que se pide.
    public static class MenuPublicoArmador
    {
        public static MenuPublicoResponseDTO Armar(MenuPublicoCacheado entrada, string baseUrl, EstadoLocal estado)
        {
            var menu = entrada.Menu;

            // La URL absoluta del logo depende del Host del request: no se cachea, se arma acá.
            // Con Blob Storage el valor guardado ya es absoluto (https://...); con almacenamiento local es
            // relativo (/uploads/...). Solo se antepone el host a los relativos.
            var logo = entrada.LogoImagenPath;
            var logoAbsoluto = logo == null
                ? null
                : logo.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                  || logo.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? logo
                    : baseUrl + logo;

            return new MenuPublicoResponseDTO
            {
                // Compatibilidad con el front desplegado: local.esActivo pasa a significar "abierto ahora".
                Local = menu.Local.ParaRespuesta(logoAbsoluto, estado.EstaAbierto),
                Categorias = menu.Categorias,
                DescuentosPedidoCompleto = menu.DescuentosPedidoCompleto,
                Estado = estado,
                Horario = menu.Horario
            };
        }
    }
}
