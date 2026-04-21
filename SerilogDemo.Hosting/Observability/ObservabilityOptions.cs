using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using Microsoft.Extensions.Configuration;

namespace SerilogDemo.Hosting.Observability;

/// <summary>
/// Holds normalized observability settings used to configure logging, tracing, and metrics exporters.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// Gets the logical service name.
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Gets the service version reported to telemetry backends.
    /// </summary>
    public required string ServiceVersion { get; init; }

    /// <summary>
    /// Gets the current service instance identifier.
    /// </summary>
    public required string InstanceId { get; init; }

    /// <summary>
    /// Gets the current application environment name.
    /// </summary>
    public required string EnvironmentName { get; init; }

    /// <summary>
    /// Gets the OTLP traces endpoint.
    /// </summary>
    public required string TracesEndpoint { get; init; }

    /// <summary>
    /// Gets the OTLP metrics endpoint.
    /// </summary>
    public required string MetricsEndpoint { get; init; }

    /// <summary>
    /// Gets the OTLP logs endpoint.
    /// </summary>
    public required string LogsEndpoint { get; init; }

    /// <summary>
    /// Gets the Serilog sink protocol name.
    /// </summary>
    public required string SerilogOtlpProtocol { get; init; }

    /// <summary>
    /// Gets the OpenTelemetry exporter protocol.
    /// </summary>
    public required OtlpExportProtocol ExportProtocol { get; init; }

    /// <summary>
    /// Gets additional resource attributes applied to telemetry signals.
    /// </summary>
    public required IReadOnlyDictionary<string, object> ResourceAttributes { get; init; }

    /// <summary>
    /// Applies service and resource metadata to an OpenTelemetry resource builder.
    /// </summary>
    public void ConfigureResource(ResourceBuilder resourceBuilder)
    {
        resourceBuilder
            .AddService(serviceName: ServiceName, serviceVersion: ServiceVersion, serviceInstanceId: InstanceId)
            .AddAttributes(ResourceAttributes.Select(item => new KeyValuePair<string, object>(item.Key, item.Value)));
    }

    /// <summary>
    /// Configures the OTLP trace exporter.
    /// </summary>
    public void ConfigureTraceExporter(OtlpExporterOptions options)
    {
        options.Endpoint = new Uri(TracesEndpoint);
        options.Protocol = ExportProtocol;
    }

    /// <summary>
    /// Configures the OTLP metric exporter.
    /// </summary>
    public void ConfigureMetricExporter(OtlpExporterOptions options)
    {
        options.Endpoint = new Uri(MetricsEndpoint);
        options.Protocol = ExportProtocol;
    }

    /// <summary>
    /// Applies derived observability settings to Serilog sink configuration.
    /// </summary>
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