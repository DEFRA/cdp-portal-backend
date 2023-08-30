using Amazon;
using Amazon.Runtime;
using Amazon.SQS;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public static class SqsSetup
{
    public static void AddSqsClient(this IServiceCollection service, , bool isDevMode)
    {
        if (isDevMode)
        {
            var awsCreds = new BasicAWSCredentials("test", "test");
            var sqsConfig = new AmazonSQSConfig
            {
                RegionEndpoint = RegionEndpoint.USEast1,
                ServiceURL = "http://127.0.0.1:4566" // Localstack's default port
            };
            var sqsClient = new AmazonSQSClient(awsCreds, sqsConfig);
            service.AddSingleton<IAmazonSQS>(sqsClient);
        }
        else
        {
            // TODO: find out what kind of config we might need
            var sqsClient = new AmazonSQSClient();
            service.AddSingleton<IAmazonSQS>(sqsClient);
        }
    }

    public static void StartSqsListeners(this IServiceProvider service)
    {
        var listeners = service.GetServices<ISqsListener>();
        foreach (var sqsListener in listeners)
        {
            Console.WriteLine($"starting {sqsListener}");
            Task.Run(() => sqsListener.ReadAsync());
        }
    }
}