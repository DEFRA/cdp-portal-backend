using System.Collections.Specialized;
using Defra.Cdp.Backend.Api.Services.Decommissioning;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.Scheduler;
using Defra.Cdp.Backend.Api.Services.Usage;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;

namespace Defra.Cdp.Backend.Api.Scheduler;

public class QuartzSchedulersHostedService(
    IServiceProvider services,
    IConfiguration config,
    ILoggerFactory loggerFactory)
    : IHostedService
{
    private IScheduler? _githubScheduler;
    private IScheduler? _repoCreationScheduler;
    private IScheduler? _decommissionScheduler;
    private IScheduler? _schedulerPollerScheduler;
    private IScheduler? _statsScheduler;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // GitHub Populate All Scheduler
        _githubScheduler = await SetupScheduler<PopulateGithubRepositories>(
            config.GetSection("Github"),
            "FetchGithubRepositories",
            cancellationToken);

        // Repository Creation Poller Scheduler
        _repoCreationScheduler = await SetupScheduler<RepositoryCreationPoller>(
            config.GetSection("RepositoriesCreation"),
            "RepositoriesCreationPoller",
            cancellationToken);

        // Decommission Scheduler
        _decommissionScheduler = await SetupScheduler<DecommissioningService>(
            config.GetSection("Decommission"),
            "DecommissionEntities",
            cancellationToken);

        // Scheduler Poller Scheduler
        _schedulerPollerScheduler = await SetupScheduler<SchedulerPoller>(
            config.GetSection("SchedulerPoller"),
            "SchedulingTasks",
            cancellationToken);
        
        // Stats Poller Scheduler
        _statsScheduler = await SetupScheduler<StatsScheduler>(
            config.GetSection("Stats"),
            "StatsScheduler",
            cancellationToken);
    }

    private async Task<IScheduler> SetupScheduler<TJob>(
        IConfigurationSection configSection,
        string jobName,
        CancellationToken ct) where TJob : IJob
    {
        var logger = loggerFactory.CreateLogger<QuartzSchedulersHostedService>();
        var props = ToQuartzProperties(configSection.GetSection("Scheduler"));
        var factory = new StdSchedulerFactory(props);
        var scheduler = await factory.GetScheduler(ct);
        scheduler.JobFactory = services.GetRequiredService<IJobFactory>();
        scheduler.ListenerManager.AddTriggerListener(new QuartzMisfireLogger(loggerFactory));

        await scheduler.Start(ct);

        var jobKey = new JobKey(jobName);
        var job = JobBuilder.Create<TJob>()
            .WithIdentity(jobKey)
            .Build();

        var trigger = TriggerBuilder.Create()
            .ForJob(jobKey)
            .WithIdentity($"{jobName}-trigger");
            
        var cronSchedule = configSection.GetValue<string?>("Cron");
        if (cronSchedule != null)
        {
            logger.LogInformation("Setting up CRON scheduler for {Job} on {Scheduler}", jobName, cronSchedule);
            trigger = trigger.WithCronSchedule(cronSchedule);
        }
        
        
        var pollIntervalSeconds = configSection.GetValue<int?>("PollIntervalSecs");
        if (pollIntervalSeconds != null)
        {
            logger.LogInformation("Setting up interval scheduler for {Job} every {Interval}s", jobName, pollIntervalSeconds);
            trigger = trigger
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(pollIntervalSeconds.Value)
                    .RepeatForever());
        }

        await scheduler.ScheduleJob(job,  trigger.Build(), ct);

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
        
        if (_statsScheduler != null)
            await _statsScheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
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