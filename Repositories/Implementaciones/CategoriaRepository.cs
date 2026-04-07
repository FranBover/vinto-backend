using Vinto.Api.Models;
using Vinto.Api.Data;
using Microsoft.EntityFrameworkCore;
using Vinto.Api.Repositories.Interfaces;

namespace Vinto.Api.Repositories.Implementaciones
{
    public class CategoriaRepository : ICategoriaRepository
    {
        private readonly AppDbContext _context;

        public CategoriaRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Categoria>> ObtenerTodas()
        {
            return await _context.Categorias.ToListAsync();
        }

        public async Task<IEnumerable<Categoria>> ObtenerPorAdministradorId(int adminId)
        {
            return await _context.Categorias
                .Where(c => c.AdministradorId == adminId)
                .ToListAsync();
        }

        public async Task<Categoria?> ObtenerPorId(int id)
        {
            return await _context.Categorias.FindAsync(id);
        }

        public async Task Crear(Categoria categoria)
        {
            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();
        }

        public async Task Actualizar(Categoria categoria)
        {
            _context.Categorias.Update(categoria);
            await _context.SaveChangesAsync();
        }

        public async Task Eliminar(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria != null)
            {
                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync();
            }
        }

    }
}
