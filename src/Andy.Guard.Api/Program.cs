using Andy.Guard.Api.Extensions;
using Andy.Guard.AspNetCore.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Scan incoming JSON requests that carry a top-level "prompt" or "text"
app.UsePromptScanning();

app.MapControllers();


app.Run();

// Expose Program for WebApplicationFactory in integration tests
namespace Andy.Guard.Api { public partial class Program { } }
