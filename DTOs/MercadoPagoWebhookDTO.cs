using System.Text.Json.Serialization;

namespace Vinto.Api.DTOs
{
    /// <summary>
    /// Payload que MercadoPago envía en los webhooks.
    /// </summary>
    public class MercadoPagoWebhookDTO
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("data")]
        public MercadoPagoWebhookData? Data { get; set; }
    }

    public class MercadoPagoWebhookData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
