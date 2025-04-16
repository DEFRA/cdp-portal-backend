using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GithubEvents;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubEvents;

public class LegacyStatusTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    private readonly IOptions<GithubEventListenerOptions> _githubEventListenerOptions =
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
            },
        Workflows = new GithubWorkflowsOptions
        {
            CreateAppConfig = "create-service.yml",
            CreateNginxUpstreams = "create-service.yml",
            CreateSquidConfig = "create-service.yml",
            CreateDashboard = "create-service.yml",
            CreateMicroservice = "create_microservice.yml",
            CreateRepository = "create_repository.yml",
            CreateJourneyTestSuite = "create_journey_test_suite.yml",
            CreatePerfTestSuite = "create_perf_test_suite.yml",
            CreateTenantService = "create-service.yml",
            ApplyTenantService = "apply.yml",
            ManualApplyTenantService = "manual.yml",
            NotifyPortal = "notify-portal.yml"
        }
    };

    [Fact]
    public async Task GithubEventUpdatesLegacyStatus()
    {
        _githubOptions.Value.Returns(_opts);

        _githubEventListenerOptions.Value.Returns(new GithubEventListenerOptions { QueueUrl = "http://localhost" });

        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "LegacyStatuses");

        var loggerFactory = new LoggerFactory();
        var legacyStatusService = new LegacyStatusService(mongoFactory, _githubOptions, loggerFactory);

        var legacyStatus = new LegacyStatus
        {
            RepositoryName = "example-repo",
            Status = Status.InProgress.ToStringValue(),
            Team = new Team { TeamId = Guid.NewGuid().ToString(), Name = "example-team" },
            Kind = CreationType.Microservice.ToStringValue(),
            ServiceTypeTemplate = "example-template"
        };

        await legacyStatusService.Create(legacyStatus, CancellationToken.None);

        var persistedStatus = await legacyStatusService.StatusForRepositoryName("example-repo", CancellationToken.None);

        Assert.NotNull(persistedStatus);
        Assert.Equal("example-repo", persistedStatus.RepositoryName);
        Assert.Equal(Status.InProgress.ToStringValue(), persistedStatus.Status);
        Assert.Equal(CreationType.Microservice.ToStringValue(), persistedStatus.Kind);
        Assert.Equal("example-template", persistedStatus.ServiceTypeTemplate);
        Assert.Equal("example-team", persistedStatus.Team.Name);

        var sqs = Substitute.For<IAmazonSQS>();
        var tenantServicesService = new TenantServicesService(mongoFactory,
            new RepositoryService(mongoFactory, loggerFactory), loggerFactory);
        var githubEventHandler = new GithubEventHandler(legacyStatusService,
            tenantServicesService,
            new DeployableArtifactsService(mongoFactory, loggerFactory),
            _githubOptions,
            loggerFactory.CreateLogger<GithubEventHandler>()
        );

        var githubEventListener = new GithubEventListener(
            sqs,
            _githubEventListenerOptions,
            _githubOptions,
            githubEventHandler,
            loggerFactory.CreateLogger<GithubEventListener>());

        await githubEventListener.Handle(
            new Message { Body = GetBody(
                "example-repo", 
                "cdp-create-workflows", 
                "create_microservice.yml" 
                ), MessageId = "1234" },
            CancellationToken.None);

        var updatedStatus = await legacyStatusService.StatusForRepositoryName("example-repo", CancellationToken.None);

        Assert.NotNull(updatedStatus);
        Assert.Equal(Status.InProgress.ToStringValue(), updatedStatus.Status);
        Assert.Equal(Status.Requested.ToStringValue(), updatedStatus.CdpCreateWorkflows.Status);
        
        
        await githubEventListener.Handle(
            new Message { Body = GetBody(
                "example-repo", 
                "cdp-create-workflows", 
                "create_microservice.yml" ,
                Status.Completed,
                Status.Success
                ), MessageId = "1234" },
            CancellationToken.None);
        
        updatedStatus = await legacyStatusService.StatusForRepositoryName("example-repo", CancellationToken.None);
        Assert.NotNull(updatedStatus);
        Assert.Equal(Status.InProgress.ToStringValue(), updatedStatus.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpCreateWorkflows.Status);
        
        
        await githubEventListener.Handle(
            new Message { Body = GetBody(
                "example-repo", 
                "cdp-nginx-upstreams", 
                "create-service.yml" ,
                Status.Completed,
                Status.Success
            ), MessageId = "1234" },
            CancellationToken.None);
        
        await githubEventListener.Handle(
            new Message { Body = GetBody(
                "example-repo", 
                "cdp-app-config", 
                "create-service.yml" ,
                Status.Completed,
                Status.Success
            ), MessageId = "1234" },
            CancellationToken.None);
        await githubEventListener.Handle(
            new Message { Body = GetBody(
                "example-repo", 
                "cdp-squid-proxy", 
                "create-service.yml" ,
                Status.Completed,
                Status.Success
            ), MessageId = "1234" },
            CancellationToken.None);
        
        await githubEventListener.Handle(
            new Message { Body = GetBody(
                "example-repo", 
                "cdp-grafana-svc", 
                "create-service.yml" ,
                Status.Completed,
                Status.Success
            ), MessageId = "1234" },
            CancellationToken.None);


        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
        {
            EventType = "create",
            Payload = new TenantServicesPayload
            {
                Environment = "management",
                Services =
                [
                    new Service
                    {
                        Name = "example-repo",
                        Zone = "public",
                        Mongo = true,
                        Redis = false,
                        ServiceCode = "example-repo",
                        TestSuite = null,
                    }
                ]
            },
            Timestamp = DateTime.Now
        }, CancellationToken.None);
        
        await githubEventListener.Handle(
            new Message { Body = """
                                 {
                                   "github_event": "workflow_run",
                                   "action": "completed",
                                   "workflow_run": {
                                     "id": 14495500809,
                                     "name": "Notify Portal",
                                     "node_id": "WFR_kwLOJVWcQM8AAAADX__KCQ",
                                     "head_branch": "main",
                                     "head_sha": "8bf63dae1b4c953495dc248daea5260ae51d2f62",
                                     "path": ".github/workflows/notify-portal.yml",
                                     "display_title": "Notify Portal",
                                     "run_number": 501,
                                     "event": "workflow_run",
                                     "status": "completed",
                                     "conclusion": "success",
                                     "workflow_id": 135795607,
                                     "check_suite_id": 37267996927,
                                     "check_suite_node_id": "CS_kwDOJVWcQM8AAAAIrViA_w",
                                     "url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/actions/runs/14495500809",
                                     "html_url": "https://github.com/DEFRA/cdp-tf-svc-infra/actions/runs/14495500809",
                                     "pull_requests": [
                                 
                                     ],
                                     "created_at": "2025-04-16T14:39:58Z",
                                     "updated_at": "2025-04-16T14:40:43Z",
                                     "actor": {
                                       "login": "reeshishah",
                                       "id": 16240196,
                                       "node_id": "MDQ6VXNlcjE2MjQwMTk2",
                                       "avatar_url": "https://avatars.githubusercontent.com/u/16240196?v=4",
                                       "gravatar_id": "",
                                       "url": "https://api.github.com/users/reeshishah",
                                       "html_url": "https://github.com/reeshishah",
                                       "followers_url": "https://api.github.com/users/reeshishah/followers",
                                       "following_url": "https://api.github.com/users/reeshishah/following{/other_user}",
                                       "gists_url": "https://api.github.com/users/reeshishah/gists{/gist_id}",
                                       "starred_url": "https://api.github.com/users/reeshishah/starred{/owner}{/repo}",
                                       "subscriptions_url": "https://api.github.com/users/reeshishah/subscriptions",
                                       "organizations_url": "https://api.github.com/users/reeshishah/orgs",
                                       "repos_url": "https://api.github.com/users/reeshishah/repos",
                                       "events_url": "https://api.github.com/users/reeshishah/events{/privacy}",
                                       "received_events_url": "https://api.github.com/users/reeshishah/received_events",
                                       "type": "User",
                                       "user_view_type": "public",
                                       "site_admin": false
                                     },
                                     "run_attempt": 1,
                                     "referenced_workflows": [
                                 
                                     ],
                                     "run_started_at": "2025-04-16T14:39:58Z",
                                     "triggering_actor": {
                                       "login": "reeshishah",
                                       "id": 16240196,
                                       "node_id": "MDQ6VXNlcjE2MjQwMTk2",
                                       "avatar_url": "https://avatars.githubusercontent.com/u/16240196?v=4",
                                       "gravatar_id": "",
                                       "url": "https://api.github.com/users/reeshishah",
                                       "html_url": "https://github.com/reeshishah",
                                       "followers_url": "https://api.github.com/users/reeshishah/followers",
                                       "following_url": "https://api.github.com/users/reeshishah/following{/other_user}",
                                       "gists_url": "https://api.github.com/users/reeshishah/gists{/gist_id}",
                                       "starred_url": "https://api.github.com/users/reeshishah/starred{/owner}{/repo}",
                                       "subscriptions_url": "https://api.github.com/users/reeshishah/subscriptions",
                                       "organizations_url": "https://api.github.com/users/reeshishah/orgs",
                                       "repos_url": "https://api.github.com/users/reeshishah/repos",
                                       "events_url": "https://api.github.com/users/reeshishah/events{/privacy}",
                                       "received_events_url": "https://api.github.com/users/reeshishah/received_events",
                                       "type": "User",
                                       "user_view_type": "public",
                                       "site_admin": false
                                     },
                                     "jobs_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/actions/runs/14495500809/jobs",
                                     "logs_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/actions/runs/14495500809/logs",
                                     "check_suite_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/check-suites/37267996927",
                                     "artifacts_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/actions/runs/14495500809/artifacts",
                                     "cancel_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/actions/runs/14495500809/cancel",
                                     "rerun_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/actions/runs/14495500809/rerun",
                                     "previous_attempt_url": null,
                                     "workflow_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/actions/workflows/135795607",
                                     "head_commit": {
                                       "id": "8bf63dae1b4c953495dc248daea5260ae51d2f62",
                                       "tree_id": "4eb40b98c59aa4bb6d8208f2b680f11b37ff4c58",
                                       "message": "Merge pull request #1138 from DEFRA/CORE-598\n\nCORE-598 Update ai-test-chat-frontend.json",
                                       "timestamp": "2025-04-16T14:36:25Z",
                                       "author": {
                                         "name": "Reeshi Shah",
                                         "email": "16240196+reeshishah@users.noreply.github.com"
                                       },
                                       "committer": {
                                         "name": "GitHub",
                                         "email": "noreply@github.com"
                                       }
                                     },
                                     "repository": {
                                       "id": 626367552,
                                       "node_id": "R_kgDOJVWcQA",
                                       "name": "cdp-tf-svc-infra",
                                       "full_name": "DEFRA/cdp-tf-svc-infra",
                                       "private": true,
                                       "owner": {
                                         "login": "DEFRA",
                                         "id": 5528822,
                                         "node_id": "MDEyOk9yZ2FuaXphdGlvbjU1Mjg4MjI=",
                                         "avatar_url": "https://avatars.githubusercontent.com/u/5528822?v=4",
                                         "gravatar_id": "",
                                         "url": "https://api.github.com/users/DEFRA",
                                         "html_url": "https://github.com/DEFRA",
                                         "followers_url": "https://api.github.com/users/DEFRA/followers",
                                         "following_url": "https://api.github.com/users/DEFRA/following{/other_user}",
                                         "gists_url": "https://api.github.com/users/DEFRA/gists{/gist_id}",
                                         "starred_url": "https://api.github.com/users/DEFRA/starred{/owner}{/repo}",
                                         "subscriptions_url": "https://api.github.com/users/DEFRA/subscriptions",
                                         "organizations_url": "https://api.github.com/users/DEFRA/orgs",
                                         "repos_url": "https://api.github.com/users/DEFRA/repos",
                                         "events_url": "https://api.github.com/users/DEFRA/events{/privacy}",
                                         "received_events_url": "https://api.github.com/users/DEFRA/received_events",
                                         "type": "Organization",
                                         "user_view_type": "public",
                                         "site_admin": false
                                       },
                                       "html_url": "https://github.com/DEFRA/cdp-tf-svc-infra",
                                       "description": "CDP Service Infrastructure",
                                       "fork": false,
                                       "url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra",
                                       "forks_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/forks",
                                       "keys_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/keys{/key_id}",
                                       "collaborators_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/collaborators{/collaborator}",
                                       "teams_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/teams",
                                       "hooks_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/hooks",
                                       "issue_events_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues/events{/number}",
                                       "events_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/events",
                                       "assignees_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/assignees{/user}",
                                       "branches_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/branches{/branch}",
                                       "tags_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/tags",
                                       "blobs_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/blobs{/sha}",
                                       "git_tags_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/tags{/sha}",
                                       "git_refs_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/refs{/sha}",
                                       "trees_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/trees{/sha}",
                                       "statuses_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/statuses/{sha}",
                                       "languages_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/languages",
                                       "stargazers_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/stargazers",
                                       "contributors_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/contributors",
                                       "subscribers_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/subscribers",
                                       "subscription_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/subscription",
                                       "commits_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/commits{/sha}",
                                       "git_commits_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/commits{/sha}",
                                       "comments_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/comments{/number}",
                                       "issue_comment_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues/comments{/number}",
                                       "contents_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/contents/{+path}",
                                       "compare_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/compare/{base}...{head}",
                                       "merges_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/merges",
                                       "archive_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/{archive_format}{/ref}",
                                       "downloads_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/downloads",
                                       "issues_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues{/number}",
                                       "pulls_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/pulls{/number}",
                                       "milestones_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/milestones{/number}",
                                       "notifications_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/notifications{?since,all,participating}",
                                       "labels_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/labels{/name}",
                                       "releases_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/releases{/id}",
                                       "deployments_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/deployments"
                                     },
                                     "head_repository": {
                                       "id": 626367552,
                                       "node_id": "R_kgDOJVWcQA",
                                       "name": "cdp-tf-svc-infra",
                                       "full_name": "DEFRA/cdp-tf-svc-infra",
                                       "private": true,
                                       "owner": {
                                         "login": "DEFRA",
                                         "id": 5528822,
                                         "node_id": "MDEyOk9yZ2FuaXphdGlvbjU1Mjg4MjI=",
                                         "avatar_url": "https://avatars.githubusercontent.com/u/5528822?v=4",
                                         "gravatar_id": "",
                                         "url": "https://api.github.com/users/DEFRA",
                                         "html_url": "https://github.com/DEFRA",
                                         "followers_url": "https://api.github.com/users/DEFRA/followers",
                                         "following_url": "https://api.github.com/users/DEFRA/following{/other_user}",
                                         "gists_url": "https://api.github.com/users/DEFRA/gists{/gist_id}",
                                         "starred_url": "https://api.github.com/users/DEFRA/starred{/owner}{/repo}",
                                         "subscriptions_url": "https://api.github.com/users/DEFRA/subscriptions",
                                         "organizations_url": "https://api.github.com/users/DEFRA/orgs",
                                         "repos_url": "https://api.github.com/users/DEFRA/repos",
                                         "events_url": "https://api.github.com/users/DEFRA/events{/privacy}",
                                         "received_events_url": "https://api.github.com/users/DEFRA/received_events",
                                         "type": "Organization",
                                         "user_view_type": "public",
                                         "site_admin": false
                                       },
                                       "html_url": "https://github.com/DEFRA/cdp-tf-svc-infra",
                                       "description": "CDP Service Infrastructure",
                                       "fork": false,
                                       "url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra",
                                       "forks_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/forks",
                                       "keys_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/keys{/key_id}",
                                       "collaborators_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/collaborators{/collaborator}",
                                       "teams_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/teams",
                                       "hooks_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/hooks",
                                       "issue_events_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues/events{/number}",
                                       "events_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/events",
                                       "assignees_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/assignees{/user}",
                                       "branches_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/branches{/branch}",
                                       "tags_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/tags",
                                       "blobs_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/blobs{/sha}",
                                       "git_tags_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/tags{/sha}",
                                       "git_refs_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/refs{/sha}",
                                       "trees_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/trees{/sha}",
                                       "statuses_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/statuses/{sha}",
                                       "languages_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/languages",
                                       "stargazers_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/stargazers",
                                       "contributors_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/contributors",
                                       "subscribers_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/subscribers",
                                       "subscription_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/subscription",
                                       "commits_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/commits{/sha}",
                                       "git_commits_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/commits{/sha}",
                                       "comments_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/comments{/number}",
                                       "issue_comment_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues/comments{/number}",
                                       "contents_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/contents/{+path}",
                                       "compare_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/compare/{base}...{head}",
                                       "merges_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/merges",
                                       "archive_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/{archive_format}{/ref}",
                                       "downloads_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/downloads",
                                       "issues_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues{/number}",
                                       "pulls_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/pulls{/number}",
                                       "milestones_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/milestones{/number}",
                                       "notifications_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/notifications{?since,all,participating}",
                                       "labels_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/labels{/name}",
                                       "releases_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/releases{/id}",
                                       "deployments_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/deployments"
                                     }
                                   },
                                   "workflow": {
                                     "id": 135795607,
                                     "node_id": "W_kwDOJVWcQM4IGBOX",
                                     "name": "Notify Portal",
                                     "path": ".github/workflows/notify-portal.yml",
                                     "state": "active",
                                     "created_at": "2024-12-31T12:17:59.000Z",
                                     "updated_at": "2024-12-31T12:17:59.000Z",
                                     "url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/actions/workflows/135795607",
                                     "html_url": "https://github.com/DEFRA/cdp-tf-svc-infra/blob/main/.github/workflows/notify-portal.yml",
                                     "badge_url": "https://github.com/DEFRA/cdp-tf-svc-infra/workflows/Notify%20Portal/badge.svg"
                                   },
                                   "repository": {
                                     "id": 626367552,
                                     "node_id": "R_kgDOJVWcQA",
                                     "name": "cdp-tf-svc-infra",
                                     "full_name": "DEFRA/cdp-tf-svc-infra",
                                     "private": true,
                                     "owner": {
                                       "login": "DEFRA",
                                       "id": 5528822,
                                       "node_id": "MDEyOk9yZ2FuaXphdGlvbjU1Mjg4MjI=",
                                       "avatar_url": "https://avatars.githubusercontent.com/u/5528822?v=4",
                                       "gravatar_id": "",
                                       "url": "https://api.github.com/users/DEFRA",
                                       "html_url": "https://github.com/DEFRA",
                                       "followers_url": "https://api.github.com/users/DEFRA/followers",
                                       "following_url": "https://api.github.com/users/DEFRA/following{/other_user}",
                                       "gists_url": "https://api.github.com/users/DEFRA/gists{/gist_id}",
                                       "starred_url": "https://api.github.com/users/DEFRA/starred{/owner}{/repo}",
                                       "subscriptions_url": "https://api.github.com/users/DEFRA/subscriptions",
                                       "organizations_url": "https://api.github.com/users/DEFRA/orgs",
                                       "repos_url": "https://api.github.com/users/DEFRA/repos",
                                       "events_url": "https://api.github.com/users/DEFRA/events{/privacy}",
                                       "received_events_url": "https://api.github.com/users/DEFRA/received_events",
                                       "type": "Organization",
                                       "user_view_type": "public",
                                       "site_admin": false
                                     },
                                     "html_url": "https://github.com/DEFRA/cdp-tf-svc-infra",
                                     "description": "CDP Service Infrastructure",
                                     "fork": false,
                                     "url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra",
                                     "forks_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/forks",
                                     "keys_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/keys{/key_id}",
                                     "collaborators_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/collaborators{/collaborator}",
                                     "teams_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/teams",
                                     "hooks_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/hooks",
                                     "issue_events_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues/events{/number}",
                                     "events_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/events",
                                     "assignees_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/assignees{/user}",
                                     "branches_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/branches{/branch}",
                                     "tags_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/tags",
                                     "blobs_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/blobs{/sha}",
                                     "git_tags_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/tags{/sha}",
                                     "git_refs_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/refs{/sha}",
                                     "trees_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/trees{/sha}",
                                     "statuses_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/statuses/{sha}",
                                     "languages_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/languages",
                                     "stargazers_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/stargazers",
                                     "contributors_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/contributors",
                                     "subscribers_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/subscribers",
                                     "subscription_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/subscription",
                                     "commits_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/commits{/sha}",
                                     "git_commits_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/git/commits{/sha}",
                                     "comments_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/comments{/number}",
                                     "issue_comment_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues/comments{/number}",
                                     "contents_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/contents/{+path}",
                                     "compare_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/compare/{base}...{head}",
                                     "merges_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/merges",
                                     "archive_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/{archive_format}{/ref}",
                                     "downloads_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/downloads",
                                     "issues_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/issues{/number}",
                                     "pulls_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/pulls{/number}",
                                     "milestones_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/milestones{/number}",
                                     "notifications_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/notifications{?since,all,participating}",
                                     "labels_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/labels{/name}",
                                     "releases_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/releases{/id}",
                                     "deployments_url": "https://api.github.com/repos/DEFRA/cdp-tf-svc-infra/deployments",
                                     "created_at": "2023-04-11T10:25:54Z",
                                     "updated_at": "2025-04-16T14:36:30Z",
                                     "pushed_at": "2025-04-16T14:36:27Z",
                                     "git_url": "git://github.com/DEFRA/cdp-tf-svc-infra.git",
                                     "ssh_url": "git@github.com:DEFRA/cdp-tf-svc-infra.git",
                                     "clone_url": "https://github.com/DEFRA/cdp-tf-svc-infra.git",
                                     "svn_url": "https://github.com/DEFRA/cdp-tf-svc-infra",
                                     "homepage": "",
                                     "size": 2628,
                                     "stargazers_count": 0,
                                     "watchers_count": 0,
                                     "language": "HCL",
                                     "has_issues": true,
                                     "has_projects": true,
                                     "has_downloads": true,
                                     "has_wiki": true,
                                     "has_pages": false,
                                     "has_discussions": false,
                                     "forks_count": 6,
                                     "mirror_url": null,
                                     "archived": false,
                                     "disabled": false,
                                     "open_issues_count": 0,
                                     "license": null,
                                     "allow_forking": true,
                                     "is_template": false,
                                     "web_commit_signoff_required": false,
                                     "topics": [
                                       "cdp"
                                     ],
                                     "visibility": "internal",
                                     "forks": 6,
                                     "open_issues": 0,
                                     "watchers": 0,
                                     "default_branch": "main",
                                     "custom_properties": {
                                 
                                     }
                                   },
                                   "organization": {
                                     "login": "DEFRA",
                                     "id": 5528822,
                                     "node_id": "MDEyOk9yZ2FuaXphdGlvbjU1Mjg4MjI=",
                                     "url": "https://api.github.com/orgs/DEFRA",
                                     "repos_url": "https://api.github.com/orgs/DEFRA/repos",
                                     "events_url": "https://api.github.com/orgs/DEFRA/events",
                                     "hooks_url": "https://api.github.com/orgs/DEFRA/hooks",
                                     "issues_url": "https://api.github.com/orgs/DEFRA/issues",
                                     "members_url": "https://api.github.com/orgs/DEFRA/members{/member}",
                                     "public_members_url": "https://api.github.com/orgs/DEFRA/public_members{/member}",
                                     "avatar_url": "https://avatars.githubusercontent.com/u/5528822?v=4",
                                     "description": "UK government department responsible for safeguarding our natural environment, supporting our food & farming industry, and sustaining a thriving rural economy."
                                   },
                                   "enterprise": {
                                     "id": 3627,
                                     "slug": "defra",
                                     "name": "Department for Environment, Food and Rural Affairs",
                                     "node_id": "MDEwOkVudGVycHJpc2UzNjI3",
                                     "avatar_url": "https://avatars.githubusercontent.com/b/3627?v=4",
                                     "description": null,
                                     "website_url": null,
                                     "html_url": "https://github.com/enterprises/defra",
                                     "created_at": "2020-07-10T22:09:01Z",
                                     "updated_at": "2025-04-14T14:10:29Z"
                                   },
                                   "sender": {
                                     "login": "reeshishah",
                                     "id": 16240196,
                                     "node_id": "MDQ6VXNlcjE2MjQwMTk2",
                                     "avatar_url": "https://avatars.githubusercontent.com/u/16240196?v=4",
                                     "gravatar_id": "",
                                     "url": "https://api.github.com/users/reeshishah",
                                     "html_url": "https://github.com/reeshishah",
                                     "followers_url": "https://api.github.com/users/reeshishah/followers",
                                     "following_url": "https://api.github.com/users/reeshishah/following{/other_user}",
                                     "gists_url": "https://api.github.com/users/reeshishah/gists{/gist_id}",
                                     "starred_url": "https://api.github.com/users/reeshishah/starred{/owner}{/repo}",
                                     "subscriptions_url": "https://api.github.com/users/reeshishah/subscriptions",
                                     "organizations_url": "https://api.github.com/users/reeshishah/orgs",
                                     "repos_url": "https://api.github.com/users/reeshishah/repos",
                                     "events_url": "https://api.github.com/users/reeshishah/events{/privacy}",
                                     "received_events_url": "https://api.github.com/users/reeshishah/received_events",
                                     "type": "User",
                                     "user_view_type": "public",
                                     "site_admin": false
                                   }
                                 }
                                 """, MessageId = "1234" },
            CancellationToken.None);

        
        updatedStatus = await legacyStatusService.StatusForRepositoryName("example-repo", CancellationToken.None);
        Assert.NotNull(updatedStatus);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpCreateWorkflows.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpNginxUpstreams?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpAppConfig?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpGrafanaSvc?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpTfSvcInfra?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpSquidProxy?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.Status);
    }


    private static string GetBody(
        string serviceRepo, 
        string workflowRepo = "cdp-tf-svc-infra",
        string workflowFile = "create-service.yml",
        Status action = Status.Requested,
        Status conclusion = Status.Requested,
        string eventType = "workflow_run")
    {
        return $@"{{
                  ""github_event"": ""{eventType}"",
                  ""action"": ""{action.ToStringValue()}"",
                  ""workflow_run"": {{
                    ""head_sha"": ""f1d2d2f924e986ac86fdf7b36c94bcdf32beec15"",
                    ""head_branch"": ""main"",
                    ""name"": ""{serviceRepo}"",
                    ""id"": 1,
                    ""conclusion"": ""{conclusion.ToStringValue()}"",
                    ""html_url"": ""http://localhost:3939/#local-stub"",
                    ""created_at"": ""2025-03-31T13:29:36.987Z"",
                    ""updated_at"": ""2025-03-31T13:29:36.987Z"",
                    ""path"": "".github/workflows/{workflowFile}"",
                    ""run_number"": 1,
                    ""head_commit"": {{
                      ""message"": ""commit message"",
                      ""author"": {{
                        ""name"": ""stub""
                      }}
                    }}
                  }},
                  ""repository"": {{
                    ""name"": ""{workflowRepo}"",
                    ""html_url"": ""http://localhost:3939/#local-stub""
                  }},
                  ""workflow"": {{
                    ""path"": "".github/workflows/{workflowFile}""
                  }}
                }}";
    }
}