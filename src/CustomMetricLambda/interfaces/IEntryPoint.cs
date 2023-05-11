using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;

namespace CustomMetric;

public interface IEntryPoint
{
    /// <summary>
    /// Entrypoint to lambda.
    /// </summary>
    /// <param name="evnt">SQS event.</param>
    /// <returns><c>true</c>, if successful.</returns>
    public Task<APIGatewayProxyResponse> HandleAsync(CloudWatchEvent<object> request, ILambdaContext context);
    
}