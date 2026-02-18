using System.Collections.Specialized;
using Defra.Cdp.Backend.Api.Services.Decommissioning;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.scheduler;
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
    private IScheduler? _schedulerPollerScheduler;

    public QuartzSchedulersHostedService(IServiceProvider services, IConfiguration config, ILoggerFactory loggerFactory)
    {
        _services = services;
        _config = config;
        _loggerFactory = loggerFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // GitHub Populate All Scheduler
        _githubScheduler = await SetupScheduler<PopulateGithubRepositories>(
            _config.GetSection("Github"),
            "FetchGithubRepositories",
            cancellationToken);

        // Repository Creation Poller Scheduler
        _repoCreationScheduler = await SetupScheduler<RepositoryCreationPoller>(
            _config.GetSection("RepositoriesCreation"),
            "RepositoriesCreationPoller",
            cancellationToken);

        // Decommission Scheduler
        _decommissionScheduler = await SetupScheduler<DecommissioningService>(
            _config.GetSection("Decommission"),
            "DecommissionEntities",
            cancellationToken);

        // Scheduler Poller Scheduler
        _schedulerPollerScheduler = await SetupScheduler<SchedulerPoller>(
            _config.GetSection("SchedulerPoller"),
            "SchedulingTasks",
            cancellationToken);
    }

    private async Task<IScheduler> SetupScheduler<TJob>(
        IConfigurationSection configSection,
        string jobName,
        CancellationToken ct) where TJob : IJob
    {
        var props = ToQuartzProperties(configSection.GetSection("Scheduler"));
        var factory = new StdSchedulerFactory(props);
        var scheduler = await factory.GetScheduler(ct);
        scheduler.JobFactory = _services.GetRequiredService<IJobFactory>();
        scheduler.ListenerManager.AddTriggerListener(new QuartzMisfireLogger(_loggerFactory));

        await scheduler.Start(ct);

        var jobKey = new JobKey(jobName);
        var job = JobBuilder.Create<TJob>()
            .WithIdentity(jobKey)
            .Build();

        var pollIntervalSeconds = configSection.GetValue<int>("PollIntervalSecs");
        var trigger = TriggerBuilder.Create()
            .ForJob(jobKey)
            .WithIdentity($"{jobName}-trigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(pollIntervalSeconds)
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger, ct);

        return scheduler;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_githubScheduler != null)
            await _githubScheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);

        if (_repoCreationScheduler != null)
            await _repoCreationScheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);

        if (_decommissionScheduler != null)
            await _decommissionScheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);

        if (_schedulerPollerScheduler != null)
            await _schedulerPollerScheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
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