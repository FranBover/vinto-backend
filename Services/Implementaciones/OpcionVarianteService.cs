using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Services.Interfaces;

namespace Vinto.Api.Services.Implementaciones
{
    public class OpcionVarianteService : IOpcionVarianteService
    {
        private readonly IOpcionVarianteRepository _opcionVarianteRepository;

        public OpcionVarianteService(IOpcionVarianteRepository opcionVarianteRepository)
        {
            _opcionVarianteRepository = opcionVarianteRepository;
        }

        public async Task<IEnumerable<OpcionVarianteResponseDTO>> ObtenerPorTipoVarianteId(int tipoVarianteId)
        {
            var opciones = await _opcionVarianteRepository.ObtenerPorTipoVarianteId(tipoVarianteId);
            return opciones.Select(o => new OpcionVarianteResponseDTO
            {
                Id = o.Id,
                Valor = o.Valor,
                Orden = o.Orden,
                TipoVarianteId = o.TipoVarianteId
            });
        }

        public async Task<OpcionVariante?> ObtenerPorId(int id)
        {
            return await _opcionVarianteRepository.ObtenerPorId(id);
        }

        public async Task<bool> ExisteValorEnTipo(int tipoVarianteId, string valor, int? excludeId = null)
        {
            return await _opcionVarianteRepository.ExisteValorEnTipo(tipoVarianteId, valor, excludeId);
        }

        public async Task<bool> TieneVariantesAsociadas(int opcionId)
        {
            return await _opcionVarianteRepository.TieneVariantesAsociadas(opcionId);
        }

        public async Task Crear(OpcionVariante opcion)
        {
            await _opcionVarianteRepository.Crear(opcion);
        }

        public async Task Actualizar(OpcionVariante opcion)
        {
            await _opcionVarianteRepository.Actualizar(opcion);
        }

        public async Task Eliminar(int id)
        {
            await _opcionVarianteRepository.Eliminar(id);
        }
    }
}
