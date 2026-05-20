using Microsoft.EntityFrameworkCore;
using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class CuponRepository : ICuponRepository
    {
        private readonly AppDbContext _context;

        public CuponRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Cupon>> ObtenerPorAdminAsync(int administradorId, bool? activo = null)
        {
            var query = _context.Cupones.Where(c => c.AdministradorId == administradorId);

            if (activo.HasValue)
                query = query.Where(c => c.Activo == activo.Value);

            return await query
                .OrderByDescending(c => c.FechaCreacion)
                .ToListAsync();
        }

        public async Task<Cupon?> ObtenerPorIdAsync(int id, int administradorId)
        {
            return await _context.Cupones
                .FirstOrDefaultAsync(c => c.Id == id && c.AdministradorId == administradorId);
        }

        public async Task<Cupon?> ObtenerPorCodigoAsync(string codigoUppercase, int administradorId)
        {
            return await _context.Cupones
                .FirstOrDefaultAsync(c => c.Codigo == codigoUppercase && c.AdministradorId == administradorId);
        }

        public async Task<Cupon> CrearAsync(Cupon cupon)
        {
            _context.Cupones.Add(cupon);
            await _context.SaveChangesAsync();
            return cupon;
        }

        public async Task<Cupon> ActualizarAsync(Cupon cupon)
        {
            _context.Cupones.Update(cupon);
            await _context.SaveChangesAsync();
            return cupon;
        }

        public async Task<Administrador?> ObtenerAdminActivoPorSlugAsync(string slugNormalizado)
        {
            var admins = await _context.Administradores
                .AsNoTracking()
                .Where(a => a.EsActivo)
                .ToListAsync();

            return admins.FirstOrDefault(a => Slugify(a.NombreLocal) == slugNormalizado);
        }

        public async Task<CuponMetricasDTO> ObtenerMetricasAsync(int cuponId)
        {
            var cupon = await _context.Cupones.FindAsync(cuponId);

            var usos = await _context.UsosCupones
                .Where(u => u.CuponId == cuponId)
                .ToListAsync();

            return new CuponMetricasDTO
            {
                CuponId = cuponId,
                Codigo = cupon!.Codigo,
                UsosTotales = usos.Count,
                UsosActivos = usos.Count(u => !u.Liberado),
                UsosLiberados = usos.Count(u => u.Liberado),
                MontoTotalDescontado = usos.Where(u => !u.Liberado).Sum(u => u.MontoDescontado),
                MontoTotalLiberado = usos.Where(u => u.Liberado).Sum(u => u.MontoDescontado),
                PrimerUso = usos.Count > 0 ? usos.Min(u => u.FechaUso) : null,
                UltimoUso = usos.Count > 0 ? usos.Max(u => u.FechaUso) : null
            };
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
    }
}
