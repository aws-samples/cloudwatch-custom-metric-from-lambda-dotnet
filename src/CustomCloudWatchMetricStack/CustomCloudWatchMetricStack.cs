using Amazon.CDK;
using Constructs;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.IAM;

namespace CustomCloudWatchMetric
{
    public class CustomCloudWatchMetricStack : Stack
    {
        internal CustomCloudWatchMetricStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var customCloudwatchMetricLambda = new Function(this, "CustomCloudwatchMetricLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_6,
                Code = Code.FromAsset("CustomMetricLambda/src/bin/Release/net6.0/publish"),
                Handler = "CustomMetricLambda::CustomMetricLambda.Function::FunctionHandler",
                Timeout = Duration.Seconds(10),
                MemorySize = 128.
                Environment = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "ECS_CLUSTER_NAME", "TODO update this" },
                    { "ECS_SERVICE_NAME", "TODO update this" },
                    { "QUEUE_URL", "TODO update this" }
                }

            });

            customCloudwatchMetricLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = new[] { "arn:aws:ecs:*" },
                Actions = new[] { "ecs:*" }
            }));

            customCloudwatchMetricLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Resources = new[] { "arn:aws:sqs:*" },
                Actions = new[] { "sqs:*" }
            }));

            customCloudwatchMetricLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "cloudwatch:PutMetricData" },
                Resources = new[] { "*" } 
            }));

            var rule = new Rule(this, "MyRule", new RuleProps
            {
                Schedule = Schedule.Rate(Duration.Minutes(2))
            });

            rule.AddTarget(new LambdaFunction(customCloudwatchMetricLambda));
        }
    }
}
