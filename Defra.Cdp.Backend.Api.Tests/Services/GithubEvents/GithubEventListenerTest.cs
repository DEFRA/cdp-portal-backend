using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.GithubEvents;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubEvents;

public class GithubEventListenerTest
{
    private readonly IAmazonSQS _sqs = Substitute.For<IAmazonSQS>();
    private readonly IGithubEventHandler _githubEventHandler = Substitute.For<IGithubEventHandler>();

    private readonly IOptions<GithubEventListenerOptions> _listenerConfig =
        Substitute.For<IOptions<GithubEventListenerOptions>>();

    private readonly IOptions<GithubOptions> _githubOptions = Substitute.For<IOptions<GithubOptions>>();

    private readonly GithubOptions _opts = new()
    {
        Organisation = "DEFRA",
        Repos =
            new GithubReposOptions
            {
                CdpTfSvcInfra = "cdp-tf-svc-infra",
                CdpAppConfig = "cdp-app-config",
                CdpAppDeployments = "cdp-app-deployments",
                CdpCreateWorkflows = "cdp-create-workflows",
                CdpGrafanaSvc = "cdp-grafana-svc",
                CdpNginxUpstreams = "cdp-nginx-upstreams",
                CdpSquidProxy = "cdp-squid-proxy"
            }
    };

    private GithubEventListener CreateListener()
    {
        var githubEventListenerOptions = new GithubEventListenerOptions();
        githubEventListenerOptions.QueueUrl = "http://localhost";

        _listenerConfig.Value.Returns(githubEventListenerOptions);
        return new GithubEventListener(
            _sqs,
            _listenerConfig,
            _githubOptions,
            _githubEventHandler,
            ConsoleLogger.CreateLogger<GithubEventListener>());
    }

    [Fact]
    public async Task WillProcessWorkflowRunEvent()
    {
        _githubOptions.Value.Returns(_opts);
        var listener = CreateListener();
        var body = GetBody();
        
        await listener.Handle(new Message { Body = body, MessageId = "1234" }, CancellationToken.None);

        await _githubEventHandler.Received(1).Handle(Arg.Is<GithubEventMessage>(x => x.GithubEvent == "workflow_run"),
            CancellationToken.None);
    }

    [Fact]
    public async Task WillNotProcessNonWorkflowRunEvent()
    {
        var listener = CreateListener();

        var body = GetBody(eventType: "not-workflow_run");

        await listener.Handle(new Message { Body = body, MessageId = "1234" }, CancellationToken.None);

        await _githubEventHandler.DidNotReceive().Handle(Arg.Any<GithubEventMessage>(), CancellationToken.None);
    }

    [Fact]
    public async Task WillNotProcessWorkflowRunEventForWebhookThatIsNotListenedTo()
    {
        _githubOptions.Value.Returns(_opts);
        var listener = CreateListener();

        var body = GetBody(repositoryName: "some-other-repo");

        await listener.Handle(new Message { Body = body, MessageId = "1234" }, CancellationToken.None);

        await _githubEventHandler.DidNotReceive().Handle(Arg.Any<GithubEventMessage>(), CancellationToken.None);
    }

    private static string GetBody(string repositoryName = "cdp-tf-svc-infra", string eventType = "workflow_run")
    {
        return $@"{{
                  ""github_event"": ""{eventType}"",
                  ""action"": ""requested"",
                  ""workflow_run"": {{
                    ""head_sha"": ""f1d2d2f924e986ac86fdf7b36c94bcdf32beec15"",
                    ""head_branch"": ""main"",
                    ""name"": ""fgasfas"",
                    ""id"": 1,
                    ""conclusion"": null,
                    ""html_url"": ""http://localhost:3939/#local-stub"",
                    ""created_at"": ""2025-03-31T13:29:36.987Z"",
                    ""updated_at"": ""2025-03-31T13:29:36.987Z"",
                    ""path"": "".github/workflows/create-service.yml"",
                    ""run_number"": 1,
                    ""head_commit"": {{
                      ""message"": ""commit message"",
                      ""author"": {{
                        ""name"": ""stub""
                      }}
                    }}
                  }},
                  ""repository"": {{
                    ""name"": ""{repositoryName}"",
                    ""html_url"": ""http://localhost:3939/#local-stub""
                  }},
                  ""workflow"": {{
                    ""path"": "".github/workflows/create-service.yml""
                  }}
                }}";
    }
}