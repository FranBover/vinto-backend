using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using Vinto.Api.Data;
using Vinto.Api.DTOs;
using Vinto.Api.Models;
using Vinto.Api.Services.Interfaces;
using Vinto.Api.Storage;

namespace Vinto.Api.Services.Implementaciones
{
    public class ImagenService : IImagenService
    {
        private readonly AppDbContext _context;
        private readonly IStorageProvider _storage;

        private static readonly HashSet<string> _allowedContentTypes = new()
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif"
        };

        private const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB

        public ImagenService(AppDbContext context, IStorageProvider storage)
        {
            _context = context;
            _storage = storage;
        }

        public async Task<ImagenResponseDTO> UploadAsync(
            IFormFile file, int adminId, string tipo, int? entidadId, int orden = 0)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("El archivo no puede estar vacío.");

            if (!_allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
                throw new ArgumentException(
                    $"Tipo de archivo no permitido: {file.ContentType}. " +
                    "Se aceptan: image/jpeg, image/png, image/webp, image/gif.");

            if (file.Length > MaxSizeBytes)
                throw new ArgumentException("El archivo supera el tamaño máximo de 5 MB.");

            var fileName = $"{Guid.NewGuid()}.webp";

            using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);

            if (image.Width > 1200)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(1200, 0),
                    Mode = ResizeMode.Max
                }));
            }

            using var outputStream = new MemoryStream();
            await image.SaveAsWebpAsync(outputStream, new WebpEncoder { Quality = 85 });
            outputStream.Position = 0;

            var url = await _storage.UploadAsync(outputStream, fileName, "image/webp");

            var imagen = new Imagen
            {
                AdministradorId = adminId,
                NombreOriginal = file.FileName,
                NombreAlmacenado = fileName,
                ContentType = "image/webp",
                TamanioBytes = outputStream.Length,
                Url = url,
                Tipo = tipo,
                EntidadId = entidadId,
                Orden = orden,
                FechaCreacion = DateTime.UtcNow
            };

            _context.Imagenes.Add(imagen);
            await _context.SaveChangesAsync();

            return ToDto(imagen);
        }

        public async Task DeleteAsync(int imagenId, int adminId)
        {
            var imagen = await _context.Imagenes
                .FirstOrDefaultAsync(i => i.Id == imagenId && i.AdministradorId == adminId);

            if (imagen == null)
                throw new KeyNotFoundException($"Imagen {imagenId} no encontrada.");

            await _storage.DeleteAsync(imagen.NombreAlmacenado);

            _context.Imagenes.Remove(imagen);
            await _context.SaveChangesAsync();
        }

        public async Task<List<ImagenResponseDTO>> GetByEntidadAsync(
            int adminId, string tipo, int? entidadId)
        {
            var imagenes = await _context.Imagenes
                .Where(i => i.AdministradorId == adminId
                         && i.Tipo == tipo
                         && i.EntidadId == entidadId)
                .OrderBy(i => i.Orden)
                .ToListAsync();

            return imagenes.Select(ToDto).ToList();
        }

        private static ImagenResponseDTO ToDto(Imagen i) => new()
        {
            Id = i.Id,
            Url = i.Url,
            Tipo = i.Tipo,
            EntidadId = i.EntidadId,
            Orden = i.Orden,
            NombreOriginal = i.NombreOriginal,
            TamanioBytes = i.TamanioBytes,
            FechaCreacion = i.FechaCreacion
        };
    }
}
