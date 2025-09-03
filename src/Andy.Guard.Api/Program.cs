using Andy.Guard.Api.Middleware;
using Andy.Guard.InputScanners.Abstractions;
using Andy.Guard.InputScanners;
using Andy.Guard.Api.Services.Abstractions;
using Andy.Guard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the library scanner (currently stubbed)
builder.Services.AddSingleton<IPromptInjectionScanner, PromptInjectionScanner>();
// Register generic scanner adapter(s) and registry
builder.Services.AddSingleton<ITextScanner, PromptInjectionTextScanner>();
builder.Services.AddSingleton<IScannerRegistry, ScannerRegistry>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Scan incoming JSON requests that carry a top-level "prompt"
app.UsePromptScanning();

app.MapControllers();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

// Expose Program for WebApplicationFactory in integration tests
namespace Andy.Guard.Api { public partial class Program { } }
