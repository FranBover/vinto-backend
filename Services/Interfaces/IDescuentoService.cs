using Vinto.Api.DTOs;

namespace Vinto.Api.Services.Interfaces
{
    public interface IDescuentoService
    {
        Task<List<DescuentoResponseDTO>> GetAllAsync(int administradorId, bool? activo = null);
        Task<DescuentoResponseDTO?> GetByIdAsync(int id, int administradorId);
        Task<DescuentoResponseDTO> CreateAsync(DescuentoCreateDTO dto, int administradorId);
        Task<DescuentoResponseDTO?> UpdateAsync(int id, DescuentoUpdateDTO dto, int administradorId);
    }
}
