
using Amazon.XRay.Recorder.Core;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.CloudWatch.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.ECS;
using Amazon.CloudWatch;
using Amazon.Lambda.CloudWatchEvents;

namespace CustomMetric;

/// <summary>
/// Class which provides DI.
/// </summary>
internal class Startup
{
    public Startup()
    {
        IConfiguration baseConfiguration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddEnvironmentVariables()
            .Build();

        Configuration = new ConfigurationBuilder()
                   .AddConfiguration(baseConfiguration)
                   .Build();

    }
    /// <summary>
    /// Configure the DI container.
    /// </summary>
    /// <returns>Service Provider.</returns>
    public IServiceProvider Configure()
    {
        IConfiguration configuration = new ConfigurationBuilder()
           .SetBasePath(Directory.GetCurrentDirectory())
           .AddEnvironmentVariables()
           .Build();

        AWSXRayRecorder.InitializeInstance(Configuration);
        
        IServiceProvider provider = ConfigureServices(Configuration);

        return provider;
    }
    public IConfiguration Configuration { get; }

    /// <summary>
    /// Configure Services.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>Service Provider.</returns>
    private IServiceProvider ConfigureServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddSingleton(Configuration);
     
        services.AddTransient<IAmazonECS, AmazonECSClient>();
        services.AddTransient<IAmazonSQS, AmazonSQSClient>();
        services.AddTransient<IAmazonCloudWatch, AmazonCloudWatchClient>();
        services.AddSingleton<IEntryPoint, EntryPoint>();
  
        return services.BuildServiceProvider();
    }
}