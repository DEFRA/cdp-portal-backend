using System.Collections.Specialized;
using Defra.Cdp.Backend.Api.Services.Decommissioning;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

namespace Defra.Cdp.Backend.Api.Schedulers;

public class QuartzSchedulersHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private IScheduler? _githubScheduler;
    private IScheduler? _repoCreationScheduler;
    private IScheduler? _decommissionScheduler;

    public QuartzSchedulersHostedService(IServiceProvider services, IConfiguration config, ILoggerFactory loggerFactory)
    {
        _services = services;
        _config = config;
        _loggerFactory = loggerFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // GitHub Populate All Scheduler
        var githubProps = ToQuartzProperties(_config.GetSection("Github:Scheduler"));
        var githubFactory = new StdSchedulerFactory(githubProps);
        _githubScheduler = await githubFactory.GetScheduler(cancellationToken);
        _githubScheduler.JobFactory = _services.GetRequiredService<IJobFactory>();
        _githubScheduler.ListenerManager.AddTriggerListener(new QuartzMisfireLogger(_loggerFactory));

        await _githubScheduler.Start(cancellationToken);

        var githubJobKey = new JobKey("FetchGithubRepositories");
        var githubJob = JobBuilder.Create<PopulateGithubRepositories>()
            .WithIdentity(githubJobKey)
            .Build();

        var githubInterval = _config.GetValue<int>("Github:PollIntervalSecs");
        var githubTrigger = TriggerBuilder.Create()
            .ForJob(githubJobKey)
            .WithIdentity("FetchGithubRepositories-trigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(githubInterval)
                .RepeatForever())
            .Build();

        await _githubScheduler.ScheduleJob(githubJob, githubTrigger, cancellationToken);
        
        // Repository Creation Poller Scheduler
        var repoCreationProps = ToQuartzProperties(_config.GetSection("RepositoriesCreation:Scheduler"));
        var repoCreationFactory = new StdSchedulerFactory(repoCreationProps);
        _repoCreationScheduler = await repoCreationFactory.GetScheduler(cancellationToken);
        _repoCreationScheduler.JobFactory = _services.GetRequiredService<IJobFactory>();
        _repoCreationScheduler.ListenerManager.AddTriggerListener(new QuartzMisfireLogger(_loggerFactory));

        await _repoCreationScheduler.Start(cancellationToken);

        var repoCreationJobKey = new JobKey("RepositoriesCreationPoller");
        var repoCreationJob = JobBuilder.Create<RepositoryCreationPoller>()
            .WithIdentity(repoCreationJobKey)
            .Build();

        var repoCreationInterval = _config.GetValue<int>("RepositoriesCreation:PollIntervalSecs");
        var repoCreationTrigger = TriggerBuilder.Create()
            .ForJob(repoCreationJob)
            .WithIdentity("RepositoriesCreationPoller-trigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(repoCreationInterval)
                .RepeatForever())
            .Build();

        await _repoCreationScheduler.ScheduleJob(repoCreationJob, repoCreationTrigger, cancellationToken);

        // Decommission Scheduler
        var decommissionProps = ToQuartzProperties(_config.GetSection("Decommission:Scheduler"));
        var decommissionFactory = new StdSchedulerFactory(decommissionProps);
        _decommissionScheduler = await decommissionFactory.GetScheduler(cancellationToken);
        _decommissionScheduler.JobFactory = _services.GetRequiredService<IJobFactory>();
        _decommissionScheduler.ListenerManager.AddTriggerListener(new QuartzMisfireLogger(_loggerFactory));

        await _decommissionScheduler.Start(cancellationToken);

        var decommissionJobKey = new JobKey("DecommissionEntities");
        var decommissionJob = JobBuilder.Create<DecommissioningService>()
            .WithIdentity(decommissionJobKey)
            .Build();

        var decommissionInterval = _config.GetValue<int>("Decommission:PollIntervalSecs");
        var decommissionTrigger = TriggerBuilder.Create()
            .ForJob(decommissionJobKey)
            .WithIdentity("DecommissionEntities-trigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(decommissionInterval)
                .RepeatForever())
            .Build();

        await _decommissionScheduler.ScheduleJob(decommissionJob, decommissionTrigger, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_githubScheduler != null)
            await _githubScheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);

        if (_decommissionScheduler != null)
            await _decommissionScheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
    }

    private static NameValueCollection ToQuartzProperties(IConfigurationSection section)
    {
        var props = new NameValueCollection();

        foreach (var child in section.GetChildren())
        {
            if (child.GetChildren().Any())
            {
                foreach (var sub in child.GetChildren())
                    props[$"{child.Key}.{sub.Key}"] = sub.Value;
            }
            else
            {
                props[child.Key] = child.Value;
            }
        }

        return props;
    }
}