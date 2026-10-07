using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Data
{
    // Única fuente de invalidación del menú público: mira lo que cada SaveChanges va a escribir
    // y, si toca algo que aparece en el menú, invalida el local dueño. Los controllers y servicios
    // no saben que existe la caché.
    //
    // Si se agrega una entidad que se muestra en el menú, hay que sumarla en ResolverAdministradores.
    // Escrituras que no pasan por SaveChanges (ExecuteUpdate/ExecuteDelete) NO se detectan acá:
    // deben invalidar a mano (ver VarianteProductoRepository.EliminarTodas).
    //
    // Se registra scoped: el estado (locales tocados) es por DbContext.
    public class MenuCacheInvalidationInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
    {
        private readonly IMenuPublicoCache _menuCache;
        private readonly HashSet<int> _pendientes = new();
        // Locales tocados dentro de una transacción explícita: se re-invalidan al commit, porque
        // un GET entre SavedChanges y el commit puede volver a cachear el dato viejo.
        private readonly HashSet<int> _pendientesTransaccion = new();

        public MenuCacheInvalidationInterceptor(IMenuPublicoCache menuCache)
        {
            _menuCache = menuCache;
        }

        // ---- SaveChanges ----

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context is AppDbContext ctx)
                ResolverAdministradores(ctx, _pendientes, esAsync: false).GetAwaiter().GetResult();
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is AppDbContext ctx)
                await ResolverAdministradores(ctx, _pendientes, esAsync: true, cancellationToken);
            return result;
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            AlGuardar(eventData.Context);
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            AlGuardar(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pendientes.Clear();

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            _pendientes.Clear();
            return Task.CompletedTask;
        }

        private void AlGuardar(DbContext? contexto)
        {
            var enTransaccion = contexto?.Database.CurrentTransaction != null;
            foreach (var adminId in _pendientes)
            {
                _menuCache.Invalidar(adminId);
                if (enTransaccion)
                    _pendientesTransaccion.Add(adminId);
            }
            _pendientes.Clear();
        }

        // ---- Transacciones explícitas ----

        public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
            => InvalidarPendientesDeTransaccion();

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            InvalidarPendientesDeTransaccion();
            return Task.CompletedTask;
        }

        public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
            => _pendientesTransaccion.Clear();

        public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            _pendientesTransaccion.Clear();
            return Task.CompletedTask;
        }

        private void InvalidarPendientesDeTransaccion()
        {
            foreach (var adminId in _pendientesTransaccion)
                _menuCache.Invalidar(adminId);
            _pendientesTransaccion.Clear();
        }

        // ---- Qué cambia el menú y de qué local ----

        private static async Task ResolverAdministradores(
            AppDbContext ctx, HashSet<int> destino, bool esAsync, CancellationToken ct = default)
        {
            var productoIds = new HashSet<int>();
            var tipoVarianteIds = new HashSet<int>();

            foreach (var entry in ctx.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                    continue;

                switch (entry.Entity)
                {
                    // Tienen AdministradorId propio
                    case Categoria c: destino.Add(c.AdministradorId); break;
                    case Producto p: destino.Add(p.AdministradorId); break;
                    case Imagen i: destino.Add(i.AdministradorId); break; // producto, categoría y logo
                    case Descuento d: destino.Add(d.AdministradorId); break;

                    // Datos del local: nombre, logo, envío, horarios, abierto/cerrado, alias, MP conectado
                    case Administrador a when entry.State != EntityState.Added: destino.Add(a.Id); break;

                    // Hijos de Producto: el local se resuelve por el producto
                    case ProductoExtra e: productoIds.Add(e.ProductoId); break;
                    case TipoVariante t: productoIds.Add(t.ProductoId); break;
                    case VarianteProducto v: productoIds.Add(v.ProductoId); break;

                    // Opción -> TipoVariante -> Producto
                    case OpcionVariante o: tipoVarianteIds.Add(o.TipoVarianteId); break;
                }
            }

            destino.Remove(0);

            if (productoIds.Count > 0)
            {
                var q = ctx.Productos.AsNoTracking().Where(p => productoIds.Contains(p.Id)).Select(p => p.AdministradorId);
                var ids = esAsync ? await q.ToListAsync(ct) : q.ToList();
                destino.UnionWith(ids);
            }

            if (tipoVarianteIds.Count > 0)
            {
                var q = ctx.TiposVariante.AsNoTracking().Where(t => tipoVarianteIds.Contains(t.Id)).Select(t => t.Producto.AdministradorId);
                var ids = esAsync ? await q.ToListAsync(ct) : q.ToList();
                destino.UnionWith(ids);
            }
        }
    }
}
