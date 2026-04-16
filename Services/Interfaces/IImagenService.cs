using Vinto.Api.DTOs;

namespace Vinto.Api.Services.Interfaces
{
    public interface IImagenService
    {
        Task<ImagenResponseDTO> UploadAsync(IFormFile file, int adminId, string tipo, int? entidadId, int orden = 0);
        Task DeleteAsync(int imagenId, int adminId);
        Task<List<ImagenResponseDTO>> GetByEntidadAsync(int adminId, string tipo, int? entidadId);
    }
}
