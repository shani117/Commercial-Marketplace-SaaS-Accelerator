using Marketplace.SaaS.Accelerator.Services.Configurations;
using Marketplace.SaaS.Accelerator.Services.Contracts;
using Marketplace.SaaS.Accelerator.Services.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.SaaS.Accelerator.CustomerSite.GraphOperations;

public static class Bootstrapper
{
    public static void AddGraphService(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GraphApiOptions>(configuration);
        // https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
        services.AddHttpClient<IGraphApiOperations, GraphApiOperationService>();
    }
}