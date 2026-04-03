using Eat_Experience.Data;
using Eat_Experience.Models;
using Eat_Experience.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Eat_Experience.Repositories.Implementaciones
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
