using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Defra.Cdp.Backend.Api.Services.Notifications;
using static Defra.Cdp.Backend.Api.Utils.CdpEnvironments;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Tests.Services.Notifications;

public class NotificationOptionsLookupTest
{
   
    [Fact]
    public void It_returns_correct_options_for_journey_tests()
    {
        var envs = new Dictionary<string, CdpTenant>
        {
            { Prod, new CdpTenant() },
            { PerfTest, new CdpTenant() },
            { Dev, new CdpTenant() },
            { Test, new CdpTenant() },
            { Management, new CdpTenant() }
        };
        
        var entity = new Entity { Name = "backend-tests", Type = Type.TestSuite, SubType = SubType.Journey, Environments = envs };

        var options = NotificationOptionLookup.FindOptionsForEntity(entity);

        Assert.Contains(options,
            n => n is
            {
                EventType: NotificationTypes.TestFailed,
                Environments: [Prod, PerfTest, Dev, Test, Management]
            });
        
        Assert.Contains(options,
            n => n is
            {
                EventType: NotificationTypes.TestPassed,
                Environments: [Prod, PerfTest, Dev, Test, Management]
            });
    }
    
    [Fact]
    public void It_returns_correct_options_for_perf_tests()
    {
        var envs = new Dictionary<string, CdpTenant>
        {
            { Prod, new CdpTenant() },
            { PerfTest, new CdpTenant() },
            { Dev, new CdpTenant() },
            { Test, new CdpTenant() },
            { Management, new CdpTenant() }
        };

        var envNames = envs.Keys.ToList();
        
        var entity = new Entity { Name = "backend-tests", Type = Type.TestSuite, SubType = SubType.Performance, Environments = envs };

        var options = NotificationOptionLookup.FindOptionsForEntity(entity);

        Assert.Contains(options,
            n => n is
            {
                EventType: NotificationTypes.TestFailed,
                Environments: [PerfTest]
            });
        Assert.Contains(options, n => n is { EventType: NotificationTypes.TestPassed, Environments: [PerfTest] });
    }

    
    [Fact]
    public void It_returns_correct_options_for_microservice()
    {
        var envs = new Dictionary<string, CdpTenant>
        {
            { Prod, new CdpTenant() },
            { PerfTest, new CdpTenant() },
            { Dev, new CdpTenant() },
            { Test, new CdpTenant() },
            { Management, new CdpTenant() }
        };
        
        var entity = new Entity { Name = "backend-service", Type = Type.Microservice, SubType = SubType.Backend, Environments = envs };

        var options = NotificationOptionLookup.FindOptionsForEntity(entity);

        Assert.Contains(options,
            n => n is
            {
                EventType: NotificationTypes.DeploymentFailed,
                Environments: [Prod, PerfTest, Dev, Test, Management]
            });
    }
}