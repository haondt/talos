using Haondt.Core.Extensions;
using Haondt.Core.Models;
using OpenTelemetry.Trace;
using System.Diagnostics;
using Talos.Api.Models;
using Talos.Core.Abstractions;
using Talos.Core.Extensions;
using SpanKind = Talos.Core.Abstractions.SpanKind;

namespace Talos.Api.Services
{
    public class OpenTelemetryTracer<TClass>(TracerProvider provider) : ITracer<TClass>
    {
        private string _spanPrefix = typeof(TClass).Name + ":";
        private Tracer _tracer = provider.GetTracer(
            typeof(TClass).Assembly.GetName().Name ?? "Default");

        public ISpan StartSpan(string name, SpanKind kind = SpanKind.Unknown, TraceLevel traceLevel = TraceLevel.Info)
        {
            var otelKind = kind switch
            {
                SpanKind.Server => OpenTelemetry.Trace.SpanKind.Server,
                SpanKind.Unknown => OpenTelemetry.Trace.SpanKind.Internal,
                _ => OpenTelemetry.Trace.SpanKind.Internal

            };
            var span = _tracer.StartActiveSpan($"{_spanPrefix}{name}", otelKind);
            span.SetAttribute("Level", traceLevel.ToString());

            return new OpenTelemetrySpan(span);
        }
        public ISpan StartRootSpan(string name, SpanKind kind = SpanKind.Unknown, TraceLevel traceLevel = TraceLevel.Info)
        {
            var otelKind = kind switch
            {
                SpanKind.Server => OpenTelemetry.Trace.SpanKind.Server,
                SpanKind.Unknown => OpenTelemetry.Trace.SpanKind.Internal,
                _ => OpenTelemetry.Trace.SpanKind.Internal

            };
            var span = _tracer.StartRootSpan($"{_spanPrefix}{name}", otelKind);
            span.SetAttribute("Level", traceLevel.ToString());

            return new OpenTelemetrySpan(span);
        }

        public Optional<string> CurrentTraceId => Activity.Current.AsOptional().As(a => a.TraceId.ToString());
        public Optional<string> CurrentSpanId => Activity.Current.AsOptional().As(a => a.SpanId.ToString());
    }
}
