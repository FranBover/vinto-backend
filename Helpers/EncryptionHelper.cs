using System.Security.Cryptography;
using System.Text;

namespace Vinto.Api.Helpers
{
    public class EncryptionHelper : IEncryptionHelper
    {
        private readonly byte[] _key;

        public EncryptionHelper(IConfiguration configuration)
        {
            var keyBase64 = configuration["Encryption:Key"]
                ?? throw new InvalidOperationException("Encryption:Key no está configurada en appsettings");

            _key = Convert.FromBase64String(keyBase64);

            if (_key.Length != 32)
                throw new InvalidOperationException("Encryption:Key debe ser de 32 bytes (256 bits) en base64");
        }

        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return plaintext;

            // AES-GCM: 12 bytes nonce, 16 bytes tag
            byte[] nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);

            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[16];

            using (var aes = new AesGcm(_key, 16))
            {
                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
            }

            // Output: nonce + tag + ciphertext, todo en base64
            byte[] output = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, output, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, output, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, output, nonce.Length + tag.Length, ciphertext.Length);

            return Convert.ToBase64String(output);
        }

        public string Decrypt(string ciphertextBase64)
        {
            if (string.IsNullOrEmpty(ciphertextBase64))
                return ciphertextBase64;

            byte[] input = Convert.FromBase64String(ciphertextBase64);

            if (input.Length < 28)
                throw new CryptographicException("Ciphertext inválido: longitud insuficiente");

            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            byte[] ciphertext = new byte[input.Length - nonce.Length - tag.Length];

            Buffer.BlockCopy(input, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(input, nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(input, nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);

            byte[] plaintext = new byte[ciphertext.Length];

            using (var aes = new AesGcm(_key, 16))
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            return Encoding.UTF8.GetString(plaintext);
        }
    }
}
