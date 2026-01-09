// App entry point
using Serilog;
using SerilogDemo;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => { configuration.WriteTo.Console(); });

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/weatherforecast", (ILogger<Program> logger) =>
{
	logger.LogInformation("GET /weatherforecast called at {Time}", DateTime.UtcNow);
	
	var forecast = WeatherForecastService.GetForecast();

	logger.LogInformation("Returning {Count} forecast items", forecast.Length);
	return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

// Run the application
try
{
	Log.Information("Starting up...");
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