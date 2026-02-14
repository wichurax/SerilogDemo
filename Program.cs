using Microsoft.EntityFrameworkCore;
using Serilog;
using SerilogDemo.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => 
    configuration.ReadFrom.Configuration(context.Configuration));

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
        Log.Error(ex, "An error occurred while seeding the database");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

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