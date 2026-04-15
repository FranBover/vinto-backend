namespace Vinto.Api.Storage
{
    public interface IStorageProvider
    {
        Task<string> UploadAsync(Stream stream, string fileName, string contentType);
        Task DeleteAsync(string fileName);
        string GetUrl(string fileName);
    }
}
