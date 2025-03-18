using Haondt.Core.Models;
using OpenTelemetry.Trace;
using Talos.Core.Abstractions;

namespace Talos.Api.Models
{
    public class OpenTelemetrySpan(TelemetrySpan inner) : ISpan
    {
        public string TraceId { get; } = inner.Context.TraceId.ToString();
        public string SpanId { get; } = inner.Context.SpanId.ToString();

        public void Dispose()
        {
            inner.Dispose();
        }

        public void SetAttribute(string key, Union<bool, bool[]> value)
        {
            if (value.Is<bool>(out var single))
                inner.SetAttribute(key, single);
            else
                inner.SetAttribute(key, value.Cast<bool[]>());
        }

        public void SetAttribute(string key, Union<string, string[]> value)
        {
            if (value.Is<string>(out var single))
                inner.SetAttribute(key, single);
            else
                inner.SetAttribute(key, value.Cast<string[]>());
        }

        public void SetAttribute(string key, Union<double, double[]> value)
        {
            if (value.Is<double>(out var single))
                inner.SetAttribute(key, single);
            else
                inner.SetAttribute(key, value.Cast<double[]>());
        }

        public void SetAttribute(string key, Union<int, int[]> value)
        {
            if (value.Is<int>(out var single))
                inner.SetAttribute(key, single);
            else
                inner.SetAttribute(key, value.Cast<int[]>());
        }

        public void SetStatusSuccess(string? description = null)
        {
            inner.SetStatus(Status.Ok.WithDescription(description));
        }

        public void SetStatusFailure(string? description = null)
        {
            inner.SetStatus(Status.Error.WithDescription(description));
        }
        public void ClearStatus()
        {
            inner.SetStatus(Status.Unset);
        }

        public void ClearAttribute(string key) => inner.SetAttribute(key, (string?)null);
    }
}
