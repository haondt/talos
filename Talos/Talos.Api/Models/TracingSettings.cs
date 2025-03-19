using OpenTelemetry.Exporter;

namespace Talos.Api.Models
{
    public class TracingSettings
    {
        public bool Enabled { get; set; }
        public string Endpoint { get; set; } = "";
        public OtlpExportProtocol Protocol { get; set; } = OtlpExportProtocol.Grpc;
        public Dictionary<string, bool> IncludeTraceLibraries { get; set; } = [];
    }
}
