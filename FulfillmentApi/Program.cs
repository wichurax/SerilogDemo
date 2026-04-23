using FulfillmentApi.Data;
using FulfillmentApi.Models;
using FulfillmentApi.Options;
using FulfillmentApi.Services;
using FulfillmentApi.Telemetry;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using Serilog;
using SerilogDemo.Hosting.Messaging;
using SerilogDemo.Hosting.Observability;

var builder = WebApplication.CreateBuilder(args);
var observability = builder.AddConfiguredObservability("fulfillment-api");

builder.Services.AddConfiguredOpenTelemetry(observability)
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation(options => options.RecordException = true)
            .AddSource(FulfillmentDiagnostics.ActivitySourceName)
            .AddOtlpExporter(observability.ConfigureTraceExporter);
    });

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("PostgreSQL connection string is not configured.");

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<FulfillmentOptions>(builder.Configuration.GetSection(FulfillmentOptions.SectionName));
builder.Services.AddHealthChecks().AddNpgSql(postgresConnectionString);
builder.Services.AddDbContext<FulfillmentDbContext>(options => options.UseNpgsql(
    postgresConnectionString,
    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__FulfillmentMigrationsHistory", "fulfillment_service")));
builder.Services.AddScoped<FulfillmentWorkflowService>();
builder.Services.AddHostedService<OrderPaidConsumerService>();
builder.Services.AddHostedService<FulfillmentOutboxPublisherService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHealthChecks("/health");

app.MapGet("/api/fulfillment/orders", async (
    string? status,
    string? userId,
    string? orderNumber,
    string? warehouse,
    int? take,
    FulfillmentWorkflowService workflowService,
    CancellationToken cancellationToken) =>
{
    if (!TryParseFulfillmentStatus(status, out var parsedStatus, out var validationError))
    {
        return Results.BadRequest(new { message = validationError });
    }

    var attempts = await workflowService.GetAttemptsAsync(
        new FulfillmentAttemptQuery(parsedStatus, userId, orderNumber, warehouse, take ?? 50),
        cancellationToken);

    return Results.Ok(attempts.Select(MapAttempt));
});

app.MapGet("/api/fulfillment/orders/{orderId:guid}", async (Guid orderId, FulfillmentDbContext dbContext, CancellationToken cancellationToken) =>
{
    var attempt = await dbContext.FulfillmentAttempts
        .AsNoTracking()
        .Include(item => item.Items)
        .FirstOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);

    return attempt is null ? Results.NotFound(new { message = $"Fulfillment attempt for order {orderId} was not found." }) : Results.Ok(MapAttempt(attempt));
});

app.MapPost("/api/fulfillment/orders/{orderId:guid}/collect", async (Guid orderId, FulfillmentActionRequest? request, FulfillmentWorkflowService workflowService, CancellationToken cancellationToken) =>
{
    var result = await workflowService.AdvanceAsync(orderId, FulfillmentStatus.Collected, request?.Message, null, cancellationToken);
    return ToHttpResult(result);
});

app.MapPost("/api/fulfillment/orders/{orderId:guid}/pack", async (Guid orderId, FulfillmentActionRequest? request, FulfillmentWorkflowService workflowService, CancellationToken cancellationToken) =>
{
    var result = await workflowService.AdvanceAsync(orderId, FulfillmentStatus.Packed, request?.Message, null, cancellationToken);
    return ToHttpResult(result);
});

app.MapPost("/api/fulfillment/orders/{orderId:guid}/ship", async (Guid orderId, ShipFulfillmentRequest? request, FulfillmentWorkflowService workflowService, CancellationToken cancellationToken) =>
{
    var result = await workflowService.AdvanceAsync(orderId, FulfillmentStatus.Shipped, request?.Message, request?.TrackingReference, cancellationToken);
    return ToHttpResult(result);
});

app.MapPost("/api/fulfillment/orders/{orderId:guid}/fail", async (Guid orderId, FulfillmentActionRequest? request, FulfillmentWorkflowService workflowService, CancellationToken cancellationToken) =>
{
    var result = await workflowService.FailAsync(orderId, request?.Message, cancellationToken);
    return ToHttpResult(result);
});

try
{
    Log.Information("Starting Fulfillment API...");
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

static FulfillmentAttemptDto MapAttempt(FulfillmentApi.Models.FulfillmentAttempt attempt)
{
    return new FulfillmentAttemptDto(
        attempt.OrderId,
        attempt.OrderNumber,
        attempt.UserId,
        attempt.Warehouse,
        attempt.DeliveryCourier,
        attempt.DeliveryOptionName,
        attempt.Status.ToString(),
        attempt.TrackingReference,
        attempt.CreatedAtUtc,
        attempt.LastProcessedAtUtc,
        attempt.CollectedAtUtc,
        attempt.PackedAtUtc,
        attempt.ShippedAtUtc,
        attempt.Items.Select(item => new FulfillmentAttemptItemDto(item.ItemId, item.ItemName, item.Quantity)).ToArray());
}

static IResult ToHttpResult(FulfillmentTransitionResult result)
{
    if (result.Succeeded && result.Attempt is not null)
    {
        return Results.Ok(MapAttempt(result.Attempt));
    }

    if (result.NotFound)
    {
        return Results.NotFound(new { message = result.Message });
    }

    return Results.Conflict(new { message = result.Message });
}

static bool TryParseFulfillmentStatus(string? status, out FulfillmentStatus? parsedStatus, out string? validationError)
{
    if (string.IsNullOrWhiteSpace(status))
    {
        parsedStatus = null;
        validationError = null;
        return true;
    }

    if (Enum.TryParse<FulfillmentStatus>(status, ignoreCase: true, out var parsed))
    {
        parsedStatus = parsed;
        validationError = null;
        return true;
    }

    parsedStatus = null;
    validationError = $"Unknown fulfillment status '{status}'.";
    return false;
}
