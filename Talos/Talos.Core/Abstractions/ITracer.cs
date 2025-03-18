using Haondt.Core.Models;

namespace Talos.Core.Abstractions
{
    public interface ITracer
    {
        Optional<string> CurrentTraceId { get; }
        Optional<string> CurrentSpanId { get; }

        public ISpan StartSpan(string name, SpanKind kind = SpanKind.Unknown);
        public ISpan StartRootSpan(string name, SpanKind kind = SpanKind.Unknown);
    }

    public interface ITracer<TClass> : ITracer
    {
    }
}
