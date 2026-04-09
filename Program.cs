using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SerilogDemo.Data;
using SerilogDemo.Telemetry;

var builder = WebApplication.CreateBuilder(args);

var serviceName = builder.Configuration["OpenTelemetry:ServiceName"]
    ?? Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
    ?? "serilogdemo-api";

var instanceId = builder.Configuration["OpenTelemetry:InstanceId"]
    ?? Environment.GetEnvironmentVariable("INSTANCE_ID")
    ?? Environment.MachineName;

var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? builder.Configuration["OpenTelemetry:OtlpEndpoint"]
    ?? "http://localhost:4318";

var otlpTracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")
    ?? builder.Configuration["OpenTelemetry:TracesEndpoint"]
    ?? GetOtlpSignalEndpoint(otlpEndpoint, "traces");

var otlpMetricsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT")
    ?? builder.Configuration["OpenTelemetry:MetricsEndpoint"]
    ?? GetOtlpSignalEndpoint(otlpEndpoint, "metrics");

var otlpLogsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT")
    ?? builder.Configuration["OpenTelemetry:LogsEndpoint"]
    ?? GetOtlpSignalEndpoint(otlpEndpoint, "logs");

var otlpProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL")
    ?? builder.Configuration["OpenTelemetry:Protocol"]
    ?? "http/protobuf";

var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
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

var otlpExportProtocol = otlpProtocol.Equals("grpc", StringComparison.OrdinalIgnoreCase)
    ? OtlpExportProtocol.Grpc
    : OtlpExportProtocol.HttpProtobuf;

void ConfigureTraceOtlpExporter(OtlpExporterOptions options)
{
    options.Endpoint = new Uri(otlpTracesEndpoint);
    options.Protocol = otlpExportProtocol;
}

void ConfigureMetricOtlpExporter(OtlpExporterOptions options)
{
    options.Endpoint = new Uri(otlpMetricsEndpoint);
    options.Protocol = otlpExportProtocol;
}

// Configure Serilog from appsettings.json
// Override OTLP endpoint from environment variable for Docker support
builder.Configuration["Serilog:WriteTo:LokiSink:Args:endpoint"] = otlpLogsEndpoint;
builder.Configuration["Serilog:WriteTo:LokiSink:Args:protocol"] = GetSerilogOtlpProtocol(otlpProtocol);
foreach (var resourceAttribute in resourceAttributes)
{
    builder.Configuration[$"Serilog:WriteTo:LokiSink:Args:resourceAttributes:{resourceAttribute.Key}"] = resourceAttribute.Value.ToString();
}
builder.Configuration["Serilog:WriteTo:LokiSink:Args:resourceAttributes:service.name"] = serviceName;
builder.Configuration["Serilog:WriteTo:LokiSink:Args:resourceAttributes:service.version"] = serviceVersion;
builder.Configuration["Serilog:WriteTo:LokiSink:Args:resourceAttributes:service.instance.id"] = instanceId;
builder.Configuration["Serilog:WriteTo:LokiSink:Args:resourceAttributes:deployment.environment"] = environmentName;

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.WithProperty("ServiceName", serviceName)
    .Enrich.WithProperty("ServiceVersion", serviceVersion)
    .Enrich.WithProperty("InstanceId", instanceId));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion, serviceInstanceId: instanceId)
            .AddAttributes(resourceAttributes.Select(item => new KeyValuePair<string, object>(item.Key, item.Value)));
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(ConfigureTraceOtlpExporter);
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(EcommerceMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddOtlpExporter(ConfigureMetricOtlpExporter);
    });

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "E-Commerce Demo API",
        Version = "v1",
        Description = "A simple e-commerce API for OpenTelemetry and Grafana observability demos"
    });
});

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("PostgreSQL connection string is not configured.");

// Add health checks
builder.Services.AddHealthChecks().AddNpgSql(postgresConnectionString);

// Configure PostgreSQL
builder.Services.AddDbContext<EcommerceDbContext>(options => options.UseNpgsql(postgresConnectionString));

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<EcommerceDbContext>>();

    try
    {
        await DbSeeder.SeedAsync(context, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health check endpoint for load balancer and container orchestration
app.MapHealthChecks("/health");

var runningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (!runningInContainer)
{
    app.UseHttpsRedirection();
}

app.MapControllers();

try
{
    Log.Information("Starting E-Commerce Demo API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static void MergeResourceAttributes(IDictionary<string, object> resourceAttributes, string? serializedAttributes)
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

static string GetOtlpSignalEndpoint(string otlpEndpoint, string signal)
{
    var expectedSuffix = $"/v1/{signal}";

    if (otlpEndpoint.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
    {
        return otlpEndpoint;
    }

    return $"{otlpEndpoint.TrimEnd('/')}{expectedSuffix}";
}

static string GetSerilogOtlpProtocol(string otlpProtocol)
{
    return otlpProtocol.Equals("grpc", StringComparison.OrdinalIgnoreCase)
        ? "Grpc"
        : "HttpProtobuf";
}