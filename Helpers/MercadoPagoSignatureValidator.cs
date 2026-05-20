using System.Security.Cryptography;
using System.Text;

namespace Vinto.Api.Helpers
{
    public class MercadoPagoSignatureValidator : IMercadoPagoSignatureValidator
    {
        private readonly byte[] _secretBytes;

        public MercadoPagoSignatureValidator(IConfiguration configuration)
        {
            var secret = configuration["MercadoPago:WebhookSecret"]
                ?? throw new InvalidOperationException("MercadoPago:WebhookSecret no configurado");

            _secretBytes = Encoding.UTF8.GetBytes(secret);
        }

        public bool Validar(string dataId, string requestId, string xSignature)
        {
            if (string.IsNullOrEmpty(dataId) ||
                string.IsNullOrEmpty(requestId) ||
                string.IsNullOrEmpty(xSignature))
            {
                return false;
            }

            // 1. Parsear el header x-signature: "ts=1704908010,v1=4b6a5c8..."
            var parts = xSignature.Split(',', StringSplitOptions.RemoveEmptyEntries);
            string? ts = null;
            string? hashRecibido = null;

            foreach (var part in parts)
            {
                var keyValue = part.Trim().Split('=', 2);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                if (key == "ts") ts = value;
                else if (key == "v1") hashRecibido = value;
            }

            if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(hashRecibido))
            {
                return false;
            }

            // 2. Construir el manifiesto en el formato exacto que MP firma
            // Formato: "id:DATA_ID;request-id:REQUEST_ID;ts:TS;"
            var manifiesto = $"id:{dataId};request-id:{requestId};ts:{ts};";

            // 3. Calcular HMAC-SHA256 del manifiesto con la clave secreta
            byte[] hashCalculado;
            using (var hmac = new HMACSHA256(_secretBytes))
            {
                hashCalculado = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifiesto));
            }

            // 4. Convertir el hash calculado a hex lowercase (formato que usa MP)
            var hashCalculadoHex = Convert.ToHexString(hashCalculado).ToLowerInvariant();

            // 5. Comparación en tiempo constante para evitar timing attacks
            var hashRecibidoBytes = Encoding.UTF8.GetBytes(hashRecibido.ToLowerInvariant());
            var hashCalculadoBytes = Encoding.UTF8.GetBytes(hashCalculadoHex);

            if (hashRecibidoBytes.Length != hashCalculadoBytes.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(hashRecibidoBytes, hashCalculadoBytes);
        }
    }
}
