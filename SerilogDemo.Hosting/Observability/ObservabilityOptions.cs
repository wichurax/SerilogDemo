using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using Microsoft.Extensions.Configuration;

namespace SerilogDemo.Hosting.Observability;

public sealed class ObservabilityOptions
{
    public required string ServiceName { get; init; }

    public required string ServiceVersion { get; init; }

    public required string InstanceId { get; init; }

    public required string EnvironmentName { get; init; }

    public required string TracesEndpoint { get; init; }

    public required string MetricsEndpoint { get; init; }

    public required string LogsEndpoint { get; init; }

    public required string SerilogOtlpProtocol { get; init; }

    public required OtlpExportProtocol ExportProtocol { get; init; }

    public required IReadOnlyDictionary<string, object> ResourceAttributes { get; init; }

    public void ConfigureResource(ResourceBuilder resourceBuilder)
    {
        resourceBuilder
            .AddService(serviceName: ServiceName, serviceVersion: ServiceVersion, serviceInstanceId: InstanceId)
            .AddAttributes(ResourceAttributes.Select(item => new KeyValuePair<string, object>(item.Key, item.Value)));
    }

    public void ConfigureTraceExporter(OtlpExporterOptions options)
    {
        options.Endpoint = new Uri(TracesEndpoint);
        options.Protocol = ExportProtocol;
    }

    public void ConfigureMetricExporter(OtlpExporterOptions options)
    {
        options.Endpoint = new Uri(MetricsEndpoint);
        options.Protocol = ExportProtocol;
    }

    public void ApplySerilogOverrides(IConfiguration configuration)
    {
        configuration["Serilog:WriteTo:LokiSink:Args:endpoint"] = LogsEndpoint;
        configuration["Serilog:WriteTo:LokiSink:Args:protocol"] = SerilogOtlpProtocol;

        foreach (var resourceAttribute in ResourceAttributes)
        {
            configuration[$"Serilog:WriteTo:LokiSink:Args:resourceAttributes:{resourceAttribute.Key}"] = resourceAttribute.Value.ToString();
        }

        configuration["Serilog:WriteTo:LokiSink:Args:resourceAttributes:service.name"] = ServiceName;
        configuration["Serilog:WriteTo:LokiSink:Args:resourceAttributes:service.version"] = ServiceVersion;
        configuration["Serilog:WriteTo:LokiSink:Args:resourceAttributes:service.instance.id"] = InstanceId;
        configuration["Serilog:WriteTo:LokiSink:Args:resourceAttributes:deployment.environment"] = EnvironmentName;
    }
}