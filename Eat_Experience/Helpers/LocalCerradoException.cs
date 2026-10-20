namespace Vinto.Api.Helpers
{
    // Se intentó iniciar un pedido con el local cerrado. Es una ValidacionException (409), pero los controllers que
    // la atrapan como 400 deben capturar ESTA antes: lleva el código y la próxima apertura para el front.
    public class LocalCerradoException : ValidacionException
    {
        public const string CodigoLocalCerrado = "LocalCerrado";

        public string Codigo => CodigoLocalCerrado;
        public DateTimeOffset? ProximoCambio { get; }

        public LocalCerradoException(string mensaje, DateTimeOffset? proximoCambio) : base(mensaje, 409)
        {
            ProximoCambio = proximoCambio;
        }
    }
}
