using Haondt.Core.Models;

namespace Talos.Core.Abstractions
{
    public interface ISpan : IDisposable
    {
        string TraceId { get; }
        string SpanId { get; }

        void ClearAttribute(string key);
        void ClearStatus();
        void SetAttribute(string key, Union<bool, bool[]> value);
        void SetAttribute(string key, Union<int, int[]> value);
        void SetAttribute(string key, Union<double, double[]> value);
        void SetAttribute(string key, Union<string, string[]> value);
        void SetStatusFailure(string? description = null);
        void SetStatusSuccess(string? description = null);
    }
}