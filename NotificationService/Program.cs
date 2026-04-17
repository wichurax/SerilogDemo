using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Trace;
using NotificationService.Data;
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
builder.Services.AddHealthChecks().AddNpgSql(postgresConnectionString);
builder.Services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(
    postgresConnectionString,
    npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__NotificationMigrationsHistory", "notification_service")));
builder.Services.AddScoped<INotificationUserProfileResolver, NotificationUserProfileResolver>();
builder.Services.AddScoped<IEmailNotificationService, FakeEmailNotificationService>();
builder.Services.AddScoped<ISmsNotificationService, FakeSmsNotificationService>();
builder.Services.AddHostedService<OrderPaidEmailConsumerService>();
builder.Services.AddHostedService<OrderPaidSmsConsumerService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapHealthChecks("/health");

app.MapGet("/api/notifications/users", async (NotificationDbContext dbContext, CancellationToken cancellationToken) =>
{
    var users = await dbContext.NotificationUserProfiles
        .AsNoTracking()
        .OrderBy(item => item.UserId)
        .Select(item => new NotificationUserProfileDto(
            item.UserId,
            item.Email,
            item.PhoneNumber,
            item.EmailEnabled,
            item.SmsEnabled,
            true))
        .ToListAsync(cancellationToken);

    return Results.Ok(users);
});

app.MapGet("/api/notifications/users/{userId}", async (string userId, INotificationUserProfileResolver resolver, CancellationToken cancellationToken) =>
{
    var profile = await resolver.ResolveAsync(userId, cancellationToken);

    return Results.Ok(new NotificationUserProfileDto(
        profile.UserId,
        profile.Email,
        profile.PhoneNumber,
        profile.EmailEnabled,
        profile.SmsEnabled,
        profile.ExistsInDatabase));
});

app.MapGet("/api/notifications/deliveries/email", async (
    NotificationDbContext dbContext,
    string? userId,
    string? orderNumber,
    string? status,
    int? take,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 50, 1, 200);

    var query = dbContext.EmailNotificationDeliveries.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(userId))
    {
        query = query.Where(item => item.UserId == userId);
    }

    if (!string.IsNullOrWhiteSpace(orderNumber))
    {
        query = query.Where(item => item.OrderNumber == orderNumber);
    }

    if (TryParseNotificationDeliveryStatus(status, out var parsedStatus))
    {
        query = query.Where(item => item.Status == parsedStatus);
    }

    var deliveries = await query
        .OrderByDescending(item => item.ProcessedAtUtc)
        .Take(limit)
        .Select(item => new EmailNotificationDeliveryDto(
            item.Id,
            item.MessageId,
            item.OrderId,
            item.OrderNumber,
            item.UserId,
            item.RecipientEmail,
            item.Status.ToString(),
            item.FailureReason,
            item.ProcessedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(deliveries);
});

app.MapGet("/api/notifications/deliveries/sms", async (
    NotificationDbContext dbContext,
    string? userId,
    string? orderNumber,
    string? status,
    int? take,
    CancellationToken cancellationToken) =>
{
    var limit = Math.Clamp(take ?? 50, 1, 200);

    var query = dbContext.SmsNotificationDeliveries.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(userId))
    {
        query = query.Where(item => item.UserId == userId);
    }

    if (!string.IsNullOrWhiteSpace(orderNumber))
    {
        query = query.Where(item => item.OrderNumber == orderNumber);
    }

    if (TryParseNotificationDeliveryStatus(status, out var parsedStatus))
    {
        query = query.Where(item => item.Status == parsedStatus);
    }

    var deliveries = await query
        .OrderByDescending(item => item.ProcessedAtUtc)
        .Take(limit)
        .Select(item => new SmsNotificationDeliveryDto(
            item.Id,
            item.MessageId,
            item.OrderId,
            item.OrderNumber,
            item.UserId,
            item.RecipientPhoneNumber,
            item.Status.ToString(),
            item.FailureReason,
            item.ProcessedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(deliveries);
});

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

static bool TryParseNotificationDeliveryStatus(string? status, out NotificationService.Models.NotificationDeliveryStatus parsedStatus)
{
    if (string.IsNullOrWhiteSpace(status))
    {
        parsedStatus = default;
        return false;
    }

    return Enum.TryParse(status, ignoreCase: true, out parsedStatus);
}

public sealed record NotificationUserProfileDto(
    string UserId,
    string Email,
    string PhoneNumber,
    bool EmailEnabled,
    bool SmsEnabled,
    bool ExistsInDatabase);

public sealed record EmailNotificationDeliveryDto(
    Guid Id,
    string MessageId,
    Guid OrderId,
    string OrderNumber,
    string UserId,
    string RecipientEmail,
    string Status,
    string? FailureReason,
    DateTime ProcessedAtUtc);

public sealed record SmsNotificationDeliveryDto(
    Guid Id,
    string MessageId,
    Guid OrderId,
    string OrderNumber,
    string UserId,
    string RecipientPhoneNumber,
    string Status,
    string? FailureReason,
    DateTime ProcessedAtUtc);
