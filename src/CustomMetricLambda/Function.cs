using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.CloudWatch.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.ECS;
using Amazon.CloudWatch;
using Amazon.Lambda.CloudWatchEvents;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace CustomMetricLambda;

public class CustomMetricLambda
{

    private readonly AmazonCloudWatchClient _cloudwatchClient;

    private readonly AmazonSQSClient _sqsClient;
    private readonly AmazonECSClient _ecsClient;
    private readonly string _clusterName;

    private readonly string _serviceName;
    private readonly string _queueUrl;

    public CustomMetricLambda()
    {

            _sqsClient = new AmazonSQSClient();
            _cloudwatchClient = new AmazonCloudWatchClient();
            _ecsClient = new AmazonECSClient();

            // Read environment variables from Lambda environment
            _clusterName = Environment.GetEnvironmentVariable("ECS_CLUSTER_NAME") ?? "";
            _serviceName = Environment.GetEnvironmentVariable("ECS_SERVICE_NAME") ?? "";
            _queueUrl = Environment.GetEnvironmentVariable("QUEUE_URL") ?? "";
    } 

    /// <summary>
    /// A function that will be invoked by EventBridge every ____  to determine if a scaling event is needed
    /// This function assumes you have 3 lambda environment variables set: ECS_CLUSTER_NAME, ECS_SERVICE_NAME and QUEUE_URL.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns>200 if successfully published cloudwatch metric. 400 if error</returns>
    public async Task<APIGatewayProxyResponse> FunctionHandler(CloudWatchEvent<object> request, ILambdaContext context)
    {
        try
        {
            // get number of messages in queue
            var approximateNumberOfMessages = await GetApproximateNumberOfMessages();

            // get number of active ECS tasks
            var numActiveTasks = await GetNumActiveTasks();

            // metric to be published. number of messages per number of active tasks
            var backlogPerTask = approximateNumberOfMessages / numActiveTasks;
            Console.WriteLine("Metric value: " + backlogPerTask);

            var metricResponse = await PublishMetric(backlogPerTask);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = metricResponse.HttpStatusCode.ToString()
            };
        }
        catch (Exception e)
        {
            var body = e.StackTrace ?? e.Message;
            return new APIGatewayProxyResponse
            {
                StatusCode = 400,
                Body = body
            };
        }

    }

    /**
        Queries the SQS queue specified in the environment variable
        returns: number of messages in the queue 
    */
    private async Task<int> GetApproximateNumberOfMessages()
    {
            var sqsRequest = new GetQueueAttributesRequest
            {
                QueueUrl = _queueUrl,
                AttributeNames =  new List<String> {"ApproximateNumberOfMessages"}
            };

            var queueAttributes = await _sqsClient.GetQueueAttributesAsync(sqsRequest);
            int approximateNumberOfMessages = int.Parse(queueAttributes.ApproximateNumberOfMessages.ToString());
            Console.WriteLine("Number of messages in the queue: " + approximateNumberOfMessages);

            return approximateNumberOfMessages;
    }

    /**
        Queries the ECS cluster for running tasks
        returns: number of running tasks in the cluster
    */
    private async Task<int> GetNumActiveTasks()
    {
        
            // query ECS to determine number of tasks
            var taskResponse =  await _ecsClient.ListTasksAsync(new Amazon.ECS.Model.ListTasksRequest
            {
                Cluster = _clusterName,
                ServiceName = _serviceName,
                DesiredStatus = "RUNNING"
            });

            var numActiveTasks = taskResponse.TaskArns.Count;
            if(numActiveTasks == 0){
                // if no tasks are running, return 1 so that we do not divide by 0
                numActiveTasks = 1;
            }
            Console.WriteLine("Number of active tasks: " + numActiveTasks);
            return numActiveTasks;
    }

    /**
        publishes the int metric to a custom cloudwatch metric called queuedepthpressure
    */
    private async Task<PutMetricDataResponse> PublishMetric(int backlogPerTask)
    {
        // publish custom metric to cloudwatch called QueueDepthPressure
        var metric = await _cloudwatchClient.PutMetricDataAsync(new PutMetricDataRequest
        {    
            
                MetricData = new List<MetricDatum>
                {
                    new MetricDatum
                    {
                        MetricName = "QueueDepthPressure",
                        Value = backlogPerTask,
                        TimestampUtc = DateTime.UtcNow
                    }
                },
            Namespace = "CustomQueueDepthMetric"
        });
        return metric;
    }
}
