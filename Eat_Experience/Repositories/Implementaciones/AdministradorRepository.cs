using Vinto.Api.Data;
using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class AdministradorRepository : IAdministradorRepository
    {
        private readonly AppDbContext _context;

        public AdministradorRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Administrador>> ObtenerTodos()
        {
            return await _context.Administradores.ToListAsync();
        }

        public async Task<Administrador?> ObtenerPorId(int id)
        {
            return await _context.Administradores.FindAsync(id);
        }

        public async Task<Administrador?> ObtenerActivoPorSlugAsync(string slug)
        {
            var slugNormalizado = SlugHelper.Normalizar(slug);
            if (slugNormalizado.Length == 0)
                return null;

            return await _context.Administradores
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.EsActivo && a.SlugLocal == slugNormalizado);
        }

        public async Task<bool> ExisteSlugAsync(string slug, int? excluirAdminId = null)
        {
            return await _context.Administradores
                .AnyAsync(a => a.SlugLocal == slug && (excluirAdminId == null || a.Id != excluirAdminId));
        }

        public async Task<string> GenerarSlugUnicoAsync(string baseSlug)
        {
            var candidato = baseSlug;
            var numero = 2;
            while (SlugHelper.EsReservado(candidato) || await ExisteSlugAsync(candidato))
                candidato = SlugHelper.ConSufijo(baseSlug, numero++);
            return candidato;
        }

        public async Task Crear(Administrador administrador)
        {
            _context.Administradores.Add(administrador);
            await _context.SaveChangesAsync();
        }

        public async Task Actualizar(Administrador administrador)
        {
            _context.Administradores.Update(administrador);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var admin = await _context.Administradores.FindAsync(id);
            if (admin != null)
            {
                _context.Administradores.Remove(admin);
                await _context.SaveChangesAsync();
            }
        }
    }
}
