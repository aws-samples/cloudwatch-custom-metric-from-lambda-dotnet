using Amazon.SQS;
using Amazon.CloudWatch;
using Amazon.ECS;
using Moq;
using Xunit;
using Amazon.ECS.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using Amazon.CloudWatch.Model;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace CustomMetric;
public class CustomMetricLambdaTests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IAmazonSQS> _mockSqsClient;
    private readonly Mock<IAmazonCloudWatch> _mockCloudWatchClient;
    private readonly Mock<IAmazonECS> _mockECSClient;

    CustomMetricLambdaTests(){
        _mockSqsClient = new Mock<IAmazonSQS>();
        _mockCloudWatchClient = new Mock<IAmazonCloudWatch>();
        _mockECSClient = new Mock<IAmazonECS>();

        _mockSqsClient.Setup(x => x.GetQueueAttributesAsync(It.IsAny<GetQueueAttributesRequest>()))
            .ReturnsAsync(new GetQueueAttributesResponse { Attributes = { { "ApproximateNumberOfMessages", "1" } } });

        _mockCloudWatchClient.Setup(x => x.PutMetricDataAsync(It.IsAny<PutMetricDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutMetricDataResponse());

        _mockECSClient.Setup(x => x.DescribeServicesAsync(It.IsAny<DescribeServicesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescribeServicesResponse { Services = new List<Service> { new Service { RunningCount = 1 } } });

        var services = new ServiceCollection();
        services.AddSingleton(_mockSqsClient.Object);
        services.AddSingleton(_mockCloudWatchClient.Object);
        services.AddSingleton(_mockECSClient.Object);

        services.AddSingleton<IEntryPoint, EntryPoint>();
        _serviceProvider = services.BuildServiceProvider();

    }

    [Fact]
    public async System.Threading.Tasks.Task MyLambdaFunctionShouldGetNumberOfMessagesFromQueueAndPublishMetric()
    {
        // Arrange
        var lambdaFunction = _serviceProvider.GetService<IEntryPoint>();

        // Act
        await lambdaFunction.HandleAsync(new CloudWatchEvent<object>(), null);

        // Assert
        _mockSqsClient.Verify(x => x.GetAttributesAsync(It.IsAny<GetQueueAttributesRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockCloudWatchClient.Verify(x => x.PutMetricDataAsync(It.IsAny<PutMetricDataRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockECSClient.Verify(x => x.DescribeServicesAsync(It.IsAny<DescribeServicesRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }


}