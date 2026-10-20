using Microsoft.Extensions.Primitives;
using Vinto.Api.DTOs;
using Vinto.Api.Models;

namespace Vinto.Api.Services.Interfaces
{
    // Datos de horario que se guardan junto al menú para calcular el estado en cada request. Son datos, no
    // un resultado: lo que depende de la hora (¿está abierto ahora?) se calcula al responder, fuera de la caché.
    public sealed record HorarioMenuCacheado(
        int AdministradorId,
        string ZonaHoraria,
        bool CerradoManualmente,
        IReadOnlyList<HorarioLocal> Horarios);

    // Lo que se cachea del menú público: el DTO ya mapeado y la ruta relativa del logo
    // (la URL absoluta depende del Host de cada request y se arma al responder).
    public sealed record MenuPublicoCacheado(MenuPublicoResponseDTO Menu, string? LogoImagenPath, HorarioMenuCacheado Horario);

    public interface IMenuPublicoCache
    {
        bool TryGet(string slugNormalizado, out MenuPublicoCacheado? entrada);

        // Token a capturar ANTES de consultar la base. Si el local se invalida mientras se arma
        // el menú, el token ya está cancelado y la entrada vieja no llega a quedar cacheada.
        IChangeToken ObtenerToken(int administradorId);

        void Set(string slugNormalizado, MenuPublicoCacheado entrada, IChangeToken token);

        // Único punto de invalidación. Lo invoca MenuCacheInvalidationInterceptor en cada
        // SaveChanges que toque algo del menú; no se llama desde controllers.
        void Invalidar(int administradorId);
    }
}
