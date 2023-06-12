using Amazon.SQS;
using Amazon.CloudWatch;
using Amazon.ECS;
using Moq;
using NUnit.Framework;
using Amazon.ECS.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using Amazon.CloudWatch.Model;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using CustomMetric;

namespace CustomMetricTest;
public class CustomMetricLambdaTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IAmazonSQS> _mockSqsClient;
    private readonly Mock<IAmazonCloudWatch> _mockCloudWatchClient;
    private readonly Mock<IAmazonECS> _mockECSClient;
    private readonly IConfiguration _configuration;
    public CustomMetricLambdaTests(){
        _mockSqsClient = new Mock<IAmazonSQS>();
        _mockCloudWatchClient = new Mock<IAmazonCloudWatch>();
        _mockECSClient = new Mock<IAmazonECS>();

        _mockSqsClient.Setup(x => x.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), default))
            .ReturnsAsync(new GetQueueAttributesResponse { Attributes = { { "ApproximateNumberOfMessages", "1" } } });
        _mockCloudWatchClient.Setup(x => x.PutMetricDataAsync(It.IsAny<PutMetricDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutMetricDataResponse());


        var mockResponse = new ListTasksResponse
        {
            TaskArns = new List<string> { "taskArn1" }
        };
        _mockECSClient.Setup(x => x.ListTasksAsync(It.IsAny<ListTasksRequest>(), default))
            .ReturnsAsync(mockResponse);

        IConfiguration baseConfiguration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddEnvironmentVariables()
            .Build();

        _configuration = new ConfigurationBuilder()
                   .AddConfiguration(baseConfiguration)
                   .Build();

        var services = new ServiceCollection();
        services.AddSingleton(_mockSqsClient.Object);
        services.AddSingleton(_mockCloudWatchClient.Object);
        services.AddSingleton(_mockECSClient.Object);
        services.AddSingleton(baseConfiguration);

        services.AddSingleton<IEntryPoint, EntryPoint>();
        _serviceProvider = services.BuildServiceProvider();



    }

    [Test]
    public async System.Threading.Tasks.Task MyLambdaFunctionShouldGetNumberOfMessagesFromQueueAndPublishMetric()
    {
        // Arrange
        var lambdaFunction = _serviceProvider.GetService<IEntryPoint>();

        // Act
        await lambdaFunction.HandleAsync(new CloudWatchEvent<object>(), null);

        // Assert
        _mockSqsClient.Verify(x => x.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockECSClient.Verify(x => x.ListTasksAsync(It.IsAny<ListTasksRequest>(), default), Times.Once);

        _mockCloudWatchClient.Verify(x => x.PutMetricDataAsync(It.Is<PutMetricDataRequest>(request =>
            request.MetricData.Count == 1 && 
            request.MetricData[0].MetricName == "QueueDepthPressure" && 
            request.MetricData[0].Value == 1 
        ), default), Times.Once);    
    
    }


}