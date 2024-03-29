using Amazon.SQS;
using LocalStack.Client.Extensions;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public static class SqsSetup
{
    public static void AddSqsClient(this IServiceCollection service, IConfiguration configuration, bool isDevMode)
    {
        if (isDevMode)
        {
            service.AddLocalStack(configuration);
            service.AddDefaultAWSOptions(configuration.GetAWSOptions());
            service.AddAwsService<IAmazonSQS>();
        }
        else
        {
            // TODO: find out what kind of config we might need
            var sqsClient = new AmazonSQSClient();
            service.AddSingleton<IAmazonSQS>(sqsClient);
        }
    }
}