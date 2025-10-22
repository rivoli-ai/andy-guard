using Andy.Guard.AspNetCore;
using Andy.Guard.AspNetCore.Middleware;
using Andy.Guard.AspNetCore.Options;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register default input scanners and registries
builder.Services.AddPromptScanning();
builder.Services.AddModelOutputScanning();
builder.Services.AddDownstreamApi("PromptInjection", builder.Configuration.GetSection("DownstreamApis:PromptInjection"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Scan incoming JSON requests that carry a top-level "prompt" or "text"
// Use middleware but do not block responses during tests; expose headers only
app.UsePromptScanning(new PromptScanningOptions { BlockOnThreat = false });

app.MapControllers();


app.Run();

// Expose Program for WebApplicationFactory in integration tests
namespace Andy.Guard.Api { public partial class Program { } }
