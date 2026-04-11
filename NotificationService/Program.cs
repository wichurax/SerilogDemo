using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using NotificationService.Data;
using NotificationService.Options;
using NotificationService.Services;
using NotificationService.Telemetry;
using Serilog;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Hosting.Observability;

var builder = WebApplication.CreateBuilder(args);
var observability = builder.AddConfiguredObservability("notification-service");

builder.Services.AddConfiguredOpenTelemetry(observability)
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddSource(NotificationDiagnostics.ActivitySourceName)
            .AddOtlpExporter(observability.ConfigureTraceExporter);
    });

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("PostgreSQL connection string is not configured.");

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<NotificationServiceOptions>(builder.Configuration.GetSection(NotificationServiceOptions.SectionName));
builder.Services.AddHealthChecks().AddNpgSql(postgresConnectionString);
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
    postgresConnectionString,
    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__NotificationMigrationsHistory", "notification_service")));
builder.Services.AddHostedService<OrderPaidConsumerService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHealthChecks("/health");

try
{
    Log.Information("Starting Notification Service...");
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
