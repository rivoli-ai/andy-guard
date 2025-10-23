using Andy.Guard.AspNetCore;
using Microsoft.Identity.Web;

namespace Andy.Guard.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
         IConfiguration configuration)
    {
        services.AddControllers();  
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddHttpClient();
        
        services.AddPromptScanning();
        services.AddModelOutputScanning();
        services.AddDownstreamApi("AndyInference", configuration.GetSection("DownstreamApis:AndyInference"));

        return services;
    }
}
