using Eat_Experience.Models;
using Eat_Experience.Repositories.Interfaces;
using Eat_Experience.Services.Interfaces;

namespace Eat_Experience.Services.Implementaciones
{
    public class AdministradorService : IAdministradorService
    {
        private readonly IAdministradorRepository _repository;

        public AdministradorService(IAdministradorRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<Administrador>> ObtenerTodos()
        {
            return await _repository.ObtenerTodos();
        }

        public async Task<Administrador?> ObtenerPorId(int id)
        {
            return await _repository.ObtenerPorId(id);
        }

        public async Task Crear(Administrador administrador)
        {
            await _repository.Crear(administrador);
        }

        public async Task Actualizar(Administrador administrador)
        {
            await _repository.Actualizar(administrador);
        }

        public async Task Eliminar(int id)
        {
            await _repository.Eliminar(id);
        }
    }
}
