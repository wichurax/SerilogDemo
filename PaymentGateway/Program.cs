using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using PaymentGateway.Data;
using PaymentGateway.Options;
using PaymentGateway.Services;
using PaymentGateway.Telemetry;
using Serilog;
using SerilogDemo.Hosting.Observability;

var builder = WebApplication.CreateBuilder(args);
var observability = builder.AddConfiguredObservability("payment-gateway");

builder.Services.AddConfiguredOpenTelemetry(observability)
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddSource(PaymentGatewayDiagnostics.ActivitySourceName)
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(observability.ConfigureTraceExporter);
    });

builder.Services.Configure<PaymentGatewaySimulationOptions>(builder.Configuration.GetSection(PaymentGatewaySimulationOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("PostgreSQL connection string is not configured.");

builder.Services.AddHealthChecks().AddNpgSql(postgresConnectionString);
builder.Services.AddDbContext<PaymentGatewayDbContext>(options => options.UseNpgsql(postgresConnectionString));
builder.Services.AddScoped<IPaymentAuthorizationService, PaymentAuthorizationService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentGatewayDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

try
{
    Log.Information("Starting Payment Gateway...");
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
