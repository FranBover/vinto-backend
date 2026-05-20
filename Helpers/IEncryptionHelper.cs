namespace Vinto.Api.Helpers
{
    public interface IEncryptionHelper
    {
        string Encrypt(string plaintext);
        string Decrypt(string ciphertext);
    }
}
