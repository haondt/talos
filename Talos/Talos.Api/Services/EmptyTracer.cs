using Haondt.Core.Models;
using System.Diagnostics;
using Talos.Core.Abstractions;

namespace Talos.Api.Services
{
    public class EmptyTracer<T> : ITracer<T>
    {
        public Optional<string> CurrentTraceId => default;

        public Optional<string> CurrentSpanId => default;

        public ISpan StartRootSpan(string name, SpanKind kind = SpanKind.Unknown, TraceLevel traceLevel = TraceLevel.Info)
        {
            return new EmptySpan();
        }

        public ISpan StartSpan(string name, SpanKind kind = SpanKind.Unknown, TraceLevel traceLevel = TraceLevel.Info)
        {
            return new EmptySpan();
        }
    }

    public class EmptySpan : ISpan
    {
        public string TraceId => "";

        public string SpanId => "";

        public void ClearAttribute(string key)
        {
        }

        public void ClearStatus()
        {
        }

        public void Dispose()
        {
        }

        public void SetAttribute(string key, Union<bool, bool[]> value)
        {
        }

        public void SetAttribute(string key, Union<int, int[]> value)
        {
        }

        public void SetAttribute(string key, Union<double, double[]> value)
        {
        }

        public void SetAttribute(string key, Union<string, string[]> value)
        {
        }

        public void SetStatusFailure(string? description = null)
        {
        }

        public void SetStatusSuccess(string? description = null)
        {
        }
    }
}
