namespace Vinto.Api.Helpers
{
    // Horarios rechazados por validación: lleva la lista completa de errores para devolverla de una vez.
    // Es una ValidacionException, así que quien ya atrapa esa excepción sigue funcionando.
    public class HorariosInvalidosException : ValidacionException
    {
        public IReadOnlyList<string> Errores { get; }

        public HorariosInvalidosException(IReadOnlyList<string> errores)
            : base("Los horarios no son válidos.")
        {
            Errores = errores;
        }
    }
}
