namespace Vinto.Api.Helpers
{
    public class ValidacionException : Exception
    {
        public int StatusCode { get; }

        public ValidacionException(string message, int statusCode = 400) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
