using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.scheduler;
using Defra.Cdp.Backend.Api.Services.scheduler.Model;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using NSubstitute;
using Quartz;

namespace Defra.Cdp.Backend.Api.Tests.Services.scheduler;

public class SchedulerPollerTests
{
    private readonly ILoggerFactory _loggerFactory = LoggerFactory.Create(b => { });

    [Fact]
    public async Task DoesNothing_WhenLockNotAcquired()
    {
        var schedulerService = Substitute.For<ISchedulerService>();
        var mongoLock = Substitute.For<IMongoLock>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var context = Substitute.For<IJobExecutionContext>();

        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);

        mongoLock.Lock(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);

        var poller = new SchedulerPoller(
            _loggerFactory,
            schedulerService,
            mongoLock,
            serviceProvider);

        await poller.Execute(context);

        await schedulerService.DidNotReceiveWithAnyArgs().FetchDueSchedules(Arg.Any<CancellationToken>());
        await mongoLock.Received(1).Unlock("processScheduledTasks", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecutesDueSchedule_AndUpdates()
    {
        var schedulerService = Substitute.For<ISchedulerService>();
        var mongoLock = Substitute.For<IMongoLock>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var context = Substitute.For<IJobExecutionContext>();

        mongoLock.Lock(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);

        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);

        var task = Substitute.For<MongoScheduleTask>();
        task.ExecuteAsync(Arg.Any<IServiceProvider>(), Arg.Any<DateTime?>(),
                Arg.Any<ILogger<object>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var schedule = new MongoSchedule(
            enabled: true,
            cron: "0 0 * * *", // dummy cron
            description: "desc",
            task: task,
            config: new MongoOnceConfig { RunAt = DateTime.UtcNow },
            new MongoUserDetails { DisplayName = "name", Id = "id" }
        ) { NextRunAt = DateTime.UtcNow };

        var id = schedule.Id;


        schedulerService.FetchDueSchedules(Arg.Any<CancellationToken>())
            .Returns([schedule]);

        var poller = new SchedulerPoller(
            _loggerFactory,
            schedulerService,
            mongoLock,
            serviceProvider);

        await poller.Execute(context);

        await task.Received(1).ExecuteAsync(
            Arg.Any<IServiceProvider>(),
            Arg.Any<DateTime?>(),
            Arg.Any<ILogger<object>>(),
            Arg.Any<CancellationToken>());

        await schedulerService.Received(1).UpdateAsync(
            id,
            Arg.Any<UpdateDefinition<MongoSchedule>>(),
            Arg.Any<CancellationToken>());

        await mongoLock.Received(1).Unlock("processScheduledTasks", Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task Continues_WhenTaskThrows_DoesNotUpdate()
    {
        var schedulerService = Substitute.For<ISchedulerService>();
        var mongoLock = Substitute.For<IMongoLock>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var context = Substitute.For<IJobExecutionContext>();

        mongoLock.Lock(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);

        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);

        var task = Substitute.For<MongoScheduleTask>();
        task.ExecuteAsync(Arg.Any<IServiceProvider>(), Arg.Any<DateTime?>(),
                Arg.Any<ILogger<object>>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new Exception("boom"));

        var schedule = new MongoSchedule(
            enabled: true,
            cron: "0 0 * * *", // dummy cron
            description: "desc",
            task: task,
            config: new MongoOnceConfig { RunAt = DateTime.UtcNow },
            new MongoUserDetails { DisplayName = "name", Id = "id" }
        ) { NextRunAt = DateTime.UtcNow };

        schedulerService.FetchDueSchedules(Arg.Any<CancellationToken>())
            .Returns([schedule]);

        var poller = new SchedulerPoller(
            _loggerFactory,
            schedulerService,
            mongoLock,
            serviceProvider);

        await poller.Execute(context);

        await schedulerService.DidNotReceive().UpdateAsync(
            Arg.Any<string>(),
            Arg.Any<UpdateDefinition<MongoSchedule>>(),
            Arg.Any<CancellationToken>());

        await mongoLock.Received(1).Unlock("processScheduledTasks", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldNotExecute_ButStillUpdates()
    {
        var schedulerService = Substitute.For<ISchedulerService>();
        var mongoLock = Substitute.For<IMongoLock>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var context = Substitute.For<IJobExecutionContext>();

        mongoLock.Lock(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);

        var task = Substitute.For<MongoScheduleTask>();
        task.ExecuteAsync(Arg.Any<IServiceProvider>(), Arg.Any<DateTime?>(), Arg.Any<ILogger<object>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var schedule = new MongoSchedule(
            enabled: true,
            cron: "0 0 * * *",
            description: "desc",
            task: task,
            config: new MongoOnceConfig { RunAt = DateTime.UtcNow.AddHours(-2) },
            new MongoUserDetails { DisplayName = "name", Id = "id" }
        ) { NextRunAt = DateTime.UtcNow.AddHours(-2) };

        schedulerService.FetchDueSchedules(Arg.Any<CancellationToken>()).Returns([schedule]);

        var poller = new SchedulerPoller(_loggerFactory, schedulerService, mongoLock, serviceProvider);

        await poller.Execute(context);

        await task.DidNotReceiveWithAnyArgs().ExecuteAsync(default, default, default, default);

        await schedulerService.Received(1).UpdateAsync(
            schedule.Id,
            Arg.Any<UpdateDefinition<MongoSchedule>>(),
            Arg.Any<CancellationToken>());

        await mongoLock.Received(1).Unlock("processScheduledTasks", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchDueSchedules_Throws_Unlocks()
    {
        var schedulerService = Substitute.For<ISchedulerService>();
        var mongoLock = Substitute.For<IMongoLock>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var context = Substitute.For<IJobExecutionContext>();

        mongoLock.Lock(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);
        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);

        schedulerService
            .When(s => s.FetchDueSchedules(Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("boom"));

        var poller = new SchedulerPoller(_loggerFactory, schedulerService, mongoLock, serviceProvider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => poller.Execute(context));

        await mongoLock.Received(1).Unlock("processScheduledTasks", Arg.Any<CancellationToken>());
    }
}