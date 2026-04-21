using OpenTelemetry;
using OpenTelemetry.Exporter;
using Serilog;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SerilogDemo.Hosting.Observability;

/// <summary>
/// Adds shared observability configuration for web applications in the solution.
/// </summary>
public static class WebApplicationBuilderObservabilityExtensions
{
    /// <summary>
    /// Builds observability options from configuration and wires Serilog defaults for the host.
    /// </summary>
    public static ObservabilityOptions AddConfiguredObservability(this WebApplicationBuilder builder, string defaultServiceName)
    {
        var serviceName = builder.Configuration["OpenTelemetry:ServiceName"]
            ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
            ?? defaultServiceName;

        var instanceId = builder.Configuration["OpenTelemetry:InstanceId"]
            ?? Environment.GetEnvironmentVariable("INSTANCE_ID")
            ?? Environment.MachineName;

        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
            ?? builder.Configuration["OpenTelemetry:OtlpEndpoint"]
            ?? "http://localhost:4318";

        var otlpProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL")
            ?? builder.Configuration["OpenTelemetry:Protocol"]
            ?? "http/protobuf";

        var serviceVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? typeof(WebApplicationBuilderObservabilityExtensions).Assembly.GetName().Version?.ToString()
            ?? "1.0.0";

        var environmentName = builder.Environment.EnvironmentName;

        var resourceAttributes = builder.Configuration
            .GetSection("OpenTelemetry:ResourceAttributes")
            .GetChildren()
            .Where(child => !string.IsNullOrWhiteSpace(child.Value))
            .ToDictionary(child => child.Key, child => (object)child.Value!);

        MergeResourceAttributes(resourceAttributes, Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES"));

        if (!resourceAttributes.ContainsKey("deployment.environment"))
        {
            resourceAttributes["deployment.environment"] = environmentName;
        }

        var observability = new ObservabilityOptions
        {
            ServiceName = serviceName,
            ServiceVersion = serviceVersion,
            InstanceId = instanceId,
            EnvironmentName = environmentName,
            TracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")
                ?? builder.Configuration["OpenTelemetry:TracesEndpoint"]
                ?? GetOtlpSignalEndpoint(otlpEndpoint, "traces"),
            MetricsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT")
                ?? builder.Configuration["OpenTelemetry:MetricsEndpoint"]
                ?? GetOtlpSignalEndpoint(otlpEndpoint, "metrics"),
            LogsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT")
                ?? builder.Configuration["OpenTelemetry:LogsEndpoint"]
                ?? GetOtlpSignalEndpoint(otlpEndpoint, "logs"),
            ExportProtocol = otlpProtocol.Equals("grpc", StringComparison.OrdinalIgnoreCase)
                ? OtlpExportProtocol.Grpc
                : OtlpExportProtocol.HttpProtobuf,
            SerilogOtlpProtocol = otlpProtocol.Equals("grpc", StringComparison.OrdinalIgnoreCase)
                ? "Grpc"
                : "HttpProtobuf",
            ResourceAttributes = resourceAttributes
        };

        observability.ApplySerilogOverrides(builder.Configuration);

        builder.Host.UseSerilog((context, _, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.WithProperty("ServiceName", observability.ServiceName)
            .Enrich.WithProperty("ServiceVersion", observability.ServiceVersion)
            .Enrich.WithProperty("InstanceId", observability.InstanceId));

        return observability;
    }

    /// <summary>
    /// Registers the shared OpenTelemetry resource configuration.
    /// </summary>
    public static OpenTelemetryBuilder AddConfiguredOpenTelemetry(this IServiceCollection services, ObservabilityOptions observability)
    {
        return services.AddOpenTelemetry()
            .ConfigureResource(observability.ConfigureResource);
    }

    private static void MergeResourceAttributes(IDictionary<string, object> resourceAttributes, string? serializedAttributes)
    {
        if (string.IsNullOrWhiteSpace(serializedAttributes))
        {
            return;
        }

        foreach (var attribute in serializedAttributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = attribute.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == attribute.Length - 1)
            {
                continue;
            }

            var key = attribute[..separatorIndex].Trim();
            var value = attribute[(separatorIndex + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
            {
                continue;
            }

            resourceAttributes[key] = value;
        }
    }

    private static string GetOtlpSignalEndpoint(string otlpEndpoint, string signal)
    {
        var expectedSuffix = $"/v1/{signal}";
        if (otlpEndpoint.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return otlpEndpoint;
        }

        return $"{otlpEndpoint.TrimEnd('/')}{expectedSuffix}";
    }
}