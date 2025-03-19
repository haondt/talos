using Haondt.Core.Models;
using System.Diagnostics;

namespace Talos.Core.Abstractions
{
    public interface ITracer
    {
        Optional<string> CurrentTraceId { get; }
        Optional<string> CurrentSpanId { get; }

        public ISpan StartSpan(string name, SpanKind kind = SpanKind.Unknown, TraceLevel traceLevel = TraceLevel.Info);
        public ISpan StartRootSpan(string name, SpanKind kind = SpanKind.Unknown, TraceLevel traceLevel = TraceLevel.Info);
    }

    public interface ITracer<TClass> : ITracer { }
}

