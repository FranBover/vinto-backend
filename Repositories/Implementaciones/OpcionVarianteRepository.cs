using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class OpcionVarianteRepository : IOpcionVarianteRepository
    {
        private readonly AppDbContext _context;

        public OpcionVarianteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<OpcionVariante>> ObtenerPorTipoVarianteId(int tipoVarianteId)
        {
            return await _context.OpcionesVariante
                .Where(o => o.TipoVarianteId == tipoVarianteId)
                .OrderBy(o => o.Orden)
                .ToListAsync();
        }

        public async Task<OpcionVariante?> ObtenerPorId(int id)
        {
            return await _context.OpcionesVariante.FindAsync(id);
        }

        public async Task<bool> ExisteValorEnTipo(int tipoVarianteId, string valor, int? excludeId = null)
        {
            return await _context.OpcionesVariante
                .AnyAsync(o =>
                    o.TipoVarianteId == tipoVarianteId &&
                    o.Valor.ToLower() == valor.ToLower() &&
                    (excludeId == null || o.Id != excludeId));
        }

        public async Task<bool> TieneVariantesAsociadas(int opcionId)
        {
            return await _context.VariantesProducto
                .AnyAsync(v => v.Opcion1Id == opcionId || v.Opcion2Id == opcionId);
        }

        public async Task Crear(OpcionVariante opcion)
        {
            _context.OpcionesVariante.Add(opcion);
            await _context.SaveChangesAsync();
        }

        public async Task Actualizar(OpcionVariante opcion)
        {
            _context.OpcionesVariante.Update(opcion);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var opcion = await _context.OpcionesVariante.FindAsync(id);
            if (opcion != null)
            {
                _context.OpcionesVariante.Remove(opcion);
                await _context.SaveChangesAsync();
            }
        }
    }
}
