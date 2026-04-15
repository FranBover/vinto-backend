using Microsoft.Extensions.FileProviders;

namespace Vinto.Api.Storage
{
    public class LocalStorageProvider : IStorageProvider
    {
        private readonly string _basePath;

        public LocalStorageProvider(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var configuredPath = configuration["Storage:Local:BasePath"] ?? "uploads";

            _basePath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);
        }

        public async Task<string> UploadAsync(Stream stream, string fileName, string contentType)
        {
            Directory.CreateDirectory(_basePath);

            var filePath = Path.Combine(_basePath, fileName);

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream);

            return GetUrl(fileName);
        }

        public Task DeleteAsync(string fileName)
        {
            var filePath = Path.Combine(_basePath, fileName);

            if (File.Exists(filePath))
                File.Delete(filePath);

            return Task.CompletedTask;
        }

        public string GetUrl(string fileName) => $"/uploads/{fileName}";
    }
}
