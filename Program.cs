using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Json;
using SerilogDemo.Data;

var builder = WebApplication.CreateBuilder(args);

// Get instance ID early for use in file paths
var instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID") ?? Environment.MachineName;

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    // Override file sinks with instance-specific paths to avoid cross-container write conflicts
    .WriteTo.File(
        path: $"Logs/{instanceId}/log-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}|InstanceId: {InstanceId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        formatter: new JsonFormatter(),
        path: $"Logs/{instanceId}/structured-.json",
        rollingInterval: RollingInterval.Day)
    .Enrich.WithProperty("InstanceId", instanceId)
    .Enrich.FromLogContext());

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "E-Commerce Demo API",
        Version = "v1",
        Description = "A simple e-commerce API for Serilog logging demonstration"
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Database=ecommerce;Username=serilog;Password=serilog123");

// Configure PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=ecommerce;Username=serilog;Password=serilog123";
builder.Services.AddDbContext<EcommerceDbContext>(options =>
    options.UseNpgsql(connectionString));

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
        Log.Error(ex, "An error occurred while seeding the database");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serilog request logging with enrichment (skip health checks to reduce noise)
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        // Reduce health check logs to debug level
        if (httpContext.Request.Path.StartsWithSegments("/health"))
            return Serilog.Events.LogEventLevel.Debug;

        // Error level for exceptions
        if (ex != null)
            return Serilog.Events.LogEventLevel.Error;

        // Warning level for client/server errors
        if (httpContext.Response.StatusCode >= 500)
            return Serilog.Events.LogEventLevel.Error;
        if (httpContext.Response.StatusCode >= 400)
            return Serilog.Events.LogEventLevel.Warning;

        return Serilog.Events.LogEventLevel.Information;
    };

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);

        // Add query string if present
        if (httpContext.Request.QueryString.HasValue)
            diagnosticContext.Set("QueryString", httpContext.Request.QueryString.Value);
    };
});

// Health check endpoint for load balancer and container orchestration
app.MapHealthChecks("/health");

app.UseHttpsRedirection();
app.MapControllers();

// Run the application
try
{
    Log.Information("Starting E-Commerce Demo API...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.Debug("Shutting down...");
    Log.CloseAndFlush();
}