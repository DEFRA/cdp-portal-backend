using Amazon.CloudWatch;
using Amazon.S3;
using Amazon.SQS;
using LocalStack.Client.Extensions;

namespace Defra.Cdp.Backend.Api.Utils.Clients;

public static class AwsClients
{

    public static void AddAwsClients(this IServiceCollection service, IConfiguration configuration, bool isDevMode)
    {
        if (isDevMode)
        {
            service.AddLocalStack(configuration);
            service.AddDefaultAWSOptions(configuration.GetAWSOptions());
            service.AddAwsService<IAmazonSQS>();
            service.AddAwsService<IAmazonS3>();
            service.AddAwsService<IAmazonCloudWatch>();
        }
        else
        {
            var sqsClient = new AmazonSQSClient();
            var s3Client = new AmazonS3Client();

            var cwConfig = new AmazonCloudWatchConfig { ProxyHost = Environment.GetEnvironmentVariable("CDP_HTTPS_PROXY"), ProxyPort = 80};
            var cwClient = new AmazonCloudWatchClient(cwConfig);

            service.AddSingleton<IAmazonSQS>(sqsClient);
            service.AddSingleton<IAmazonS3>(s3Client);
            service.AddSingleton<IAmazonCloudWatch>(cwClient);
        }
    }
}