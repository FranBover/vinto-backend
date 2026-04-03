using Eat_Experience.Models;
using Eat_Experience.Data;
using Microsoft.EntityFrameworkCore;
using Eat_Experience.Repositories.Interfaces;

namespace Eat_Experience.Repositories.Implementaciones
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
