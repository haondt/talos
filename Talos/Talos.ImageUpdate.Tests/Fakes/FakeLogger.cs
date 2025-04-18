using Microsoft.Extensions.Logging;

namespace Talos.ImageUpdate.Tests.Fakes
{
    internal class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance = new();
        public void Dispose()
        {
        }
    }

    internal class FakeLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return EmptyDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}