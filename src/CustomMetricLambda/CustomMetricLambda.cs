using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.CloudWatchEvents;
using Microsoft.Extensions.DependencyInjection;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CustomMetricLambda;

public class CustomMetricLambda
{

    private readonly IServiceProvider _serviceProvider;

    public CustomMetricLambda()
    {
        _serviceProvider = new Startup().Configure();
    } 

    /// <summary>
    /// A function that will be invoked by EventBridge every 2 minutes to determine if a scaling event is needed
    /// This function assumes you have 3 lambda environment variables set: ECS_CLUSTER_NAME, ECS_SERVICE_NAME and QUEUE_URL.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns>200 if successfully published cloudwatch metric. 400 if error</returns>
    public async Task<APIGatewayProxyResponse> FunctionHandler(CloudWatchEvent<object> request, ILambdaContext context)
    {
        var entrypoint = _serviceProvider.GetRequiredService<IEntryPoint>();
        return await entrypoint.HandleAsync(request, context);
    }
}