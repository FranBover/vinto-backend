using Microsoft.Extensions.Logging;

namespace Eat_Experience.Tests;

// Logger de captura para verificar qué se loguea, sin sumar paquetes de test.
internal sealed class FakeLogger : ILogger
{
    public sealed record Entrada(LogLevel Nivel, string Mensaje, Exception? Excepcion);

    public List<Entrada> Entradas { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entradas.Add(new Entrada(logLevel, formatter(state, exception), exception));
}
