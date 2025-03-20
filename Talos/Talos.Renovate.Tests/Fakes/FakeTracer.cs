using Haondt.Core.Models;
using System.Diagnostics;
using Talos.Core.Abstractions;

namespace Talos.Renovate.Tests.Fakes
{
    public class FakeTracer<T> : ITracer<T>
    {
        public Optional<string> CurrentTraceId => new();

        public Optional<string> CurrentSpanId => new();

        public ISpan StartRootSpan(string name, SpanKind kind = SpanKind.Unknown, TraceLevel traceLevel = TraceLevel.Info)
        {
            return new FakeSpan();
        }

        public ISpan StartSpan(string name, SpanKind kind = SpanKind.Unknown, TraceLevel traceLevel = TraceLevel.Info)
        {
            return new FakeSpan();
        }
    }

    public class FakeSpan : ISpan
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
