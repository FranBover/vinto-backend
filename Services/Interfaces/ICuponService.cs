using Vinto.Api.DTOs;

namespace Vinto.Api.Services.Interfaces
{
    public interface ICuponService
    {
        Task<List<CuponResponseDTO>> GetAllAsync(int administradorId, bool? activo = null);
        Task<CuponResponseDTO?> GetByIdAsync(int id, int administradorId);
        Task<CuponResponseDTO> CreateAsync(CuponCreateDTO dto, int administradorId);
        Task<CuponResponseDTO?> UpdateAsync(int id, CuponUpdateDTO dto, int administradorId);
        Task<CuponMetricasDTO?> GetMetricasAsync(int id, int administradorId);
        Task<ValidarCuponResponseDTO> ValidarCuponPublicoAsync(string slug, ValidarCuponRequestDTO request);
    }
}
