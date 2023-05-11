using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;

using Amazon.CloudWatch.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.ECS;
using Amazon.CloudWatch;
using Microsoft.Extensions.Configuration;

namespace CustomMetricLambda;

internal class EntryPoint : IEntryPoint
{

    private readonly IAmazonCloudWatch _cloudwatchClient;
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonECS _ecsClient;
    private readonly IConfiguration _configuration;

    public EntryPoint(IConfiguration configuration, IAmazonCloudWatch cloudwatchClient, IAmazonSQS sqsClient, IAmazonECS ecsClient){

        _configuration = configuration;
        _cloudwatchClient = cloudwatchClient;
        _sqsClient = sqsClient;
        _ecsClient = ecsClient;

    }
    /// <summary>
    /// A function that will be invoked by EventBridge every ____  to determine if a scaling event is needed
    /// This function assumes you have 3 lambda environment variables set: ECS_CLUSTER_NAME, ECS_SERVICE_NAME and QUEUE_URL.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns>200 if successfully published cloudwatch metric. 400 if error</returns>
    public async Task<APIGatewayProxyResponse> HandleAsync(CloudWatchEvent<object> request, ILambdaContext context)
    {
        try
        {
            // get number of messages in queue
            var approximateNumberOfMessages = await GetApproximateNumberOfMessages();

            // get number of active ECS tasks
            var numActiveTasks = 1; //await GetNumActiveTasks();

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
                QueueUrl = _configuration.GetValue<string>("QUEUE_URL"),
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
                Cluster = _configuration.GetValue<string>("ECS_CLUSTER_NAME"),
                ServiceName = _configuration.GetValue<string>("ECS_SERVICE_NAME"),
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
