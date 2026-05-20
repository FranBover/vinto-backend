namespace Vinto.Api.Helpers
{
    public interface IMercadoPagoSignatureValidator
    {
        /// <summary>
        /// Valida la firma HMAC-SHA256 de un webhook de MercadoPago.
        /// </summary>
        /// <param name="dataId">El data.id extraído del body del webhook.</param>
        /// <param name="requestId">El valor del header x-request-id.</param>
        /// <param name="xSignature">El valor del header x-signature (formato: "ts=...,v1=...").</param>
        /// <returns>true si la firma es válida, false si no.</returns>
        bool Validar(string dataId, string requestId, string xSignature);
    }
}
