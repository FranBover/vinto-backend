using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class MenuPublicoCache : IMenuPublicoCache
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        private readonly IMemoryCache _cache;

        // Una fuente de cancelación por local: cancelarla expulsa todas las entradas de ese local,
        // sin importar bajo qué slug estén (cubre también el cambio de slug).
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _fuentes = new();

        public MenuPublicoCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        private static string Clave(string slugNormalizado) => $"menu-publico:{slugNormalizado}";

        public bool TryGet(string slugNormalizado, out MenuPublicoCacheado? entrada)
            => _cache.TryGetValue(Clave(slugNormalizado), out entrada);

        public IChangeToken ObtenerToken(int administradorId)
        {
            var fuente = _fuentes.GetOrAdd(administradorId, _ => new CancellationTokenSource());
            return new CancellationChangeToken(fuente.Token);
        }

        public void Set(string slugNormalizado, MenuPublicoCacheado entrada, IChangeToken token)
        {
            var opciones = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl };
            opciones.AddExpirationToken(token);
            _cache.Set(Clave(slugNormalizado), entrada, opciones);
        }

        public void Invalidar(int administradorId)
        {
            if (_fuentes.TryRemove(administradorId, out var fuente))
                fuente.Cancel();
        }
    }
}
