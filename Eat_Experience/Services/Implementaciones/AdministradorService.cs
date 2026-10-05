using Vinto.Api.Helpers;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
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

        public async Task<string> ValidarNuevoSlugAsync(int adminId, string slug)
        {
            var normalizado = SlugHelper.Normalizar(slug);

            if (!SlugHelper.EsFormatoValido(normalizado))
                throw new ValidacionException(
                    $"El slug no es válido. Usá solo letras minúsculas sin acentos, números y guiones simples " +
                    $"(sin guiones al inicio ni al final), hasta {SlugHelper.MaxLength} caracteres.");

            if (SlugHelper.EsReservado(normalizado))
                throw new ValidacionException("Ese slug está reservado por el sistema. Elegí otro.");

            if (await _repository.ExisteSlugAsync(normalizado, adminId))
                throw new ValidacionException("Ese slug ya está en uso por otro local.");

            return normalizado;
        }
    }
}
