using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SerilogDemo.Options;
using SerilogDemo.Services.Checkout;
using SerilogDemo.Services.Inventory;
using SerilogDemo.Services.Messaging;
using SerilogDemo.Services.Payments;
using Serilog;
using SerilogDemo.Data;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Hosting.Observability;
using SerilogDemo.Telemetry;

var builder = WebApplication.CreateBuilder(args);
var observability = builder.AddConfiguredObservability("serilogdemo-api");

builder.Services.AddConfiguredOpenTelemetry(observability)
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddSource(EcommerceDiagnostics.ActivitySourceName)
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(observability.ConfigureTraceExporter);
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(EcommerceMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(observability.ConfigureMetricExporter);
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

builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection(PaymentGatewayOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<WarehouseOptions>(builder.Configuration.GetSection(WarehouseOptions.SectionName));
builder.Services.AddHttpClient<IPaymentGatewayClient, PaymentGatewayClient>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddHostedService<OutboxPublisherService>();
builder.Services.AddHostedService<FulfillmentProgressConsumerService>();

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
    var warehouseOptions = scope.ServiceProvider.GetRequiredService<IOptions<WarehouseOptions>>();

    try
    {
        await DbSeeder.SeedAsync(context, logger, warehouseOptions);
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
