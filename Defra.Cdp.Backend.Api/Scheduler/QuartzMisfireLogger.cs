namespace Defra.Cdp.Backend.Api.Schedulers;

using Quartz;
using Quartz.Listener;

public class QuartzMisfireLogger(ILoggerFactory loggerFactory) : TriggerListenerSupport
{
    private readonly ILogger<QuartzMisfireLogger> _logger = loggerFactory.CreateLogger<QuartzMisfireLogger>();

    public override string Name => "GlobalQuartzMisfireLogger";

    public override Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Trigger misfired: {TriggerKey} - next fire time: {NextFireTimeUtc}",
            trigger.Key,
            trigger.GetNextFireTimeUtc()?.ToString("o") ?? "none");

        return Task.CompletedTask;
    }
}