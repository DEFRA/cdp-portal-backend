using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.Endpoints.Validators;
using Defra.Cdp.Backend.Api.Services.Notifications;

namespace Defra.Cdp.Backend.Api.Tests.Endpoints.Validators;

public class CreateNotificationRuleRequestValidatorTest
{
    [Fact]
    public void TestValidInputs()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateNotificationRuleRequest
        {
            Entity = "foo",
            EventType = NotificationEventTypes.TestRunPassed.Type,
            Conditions = new Dictionary<string, string> { { "Environment", "dev" } }
        });
        
        Assert.True(res.IsValid);
    }
    
    [Fact]
    public void TestValidInputsNoEnv()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateNotificationRuleRequest
        {
            Entity = "foo",
            EventType = NotificationEventTypes.TestRunPassed.Type,
            Conditions = new Dictionary<string, string>() 
        });
        
        Assert.True(res.IsValid);
    }
    
    [Fact]
    public void TestInvalidInputsNoEnv()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateNotificationRuleRequest
        {
            Entity = "foo",
            EventType = NotificationEventTypes.TestRunPassed.Type,
            Conditions = new Dictionary<string, string> { {"Pigeon", "rock-dove"}} 
        });
        
        Assert.False(res.IsValid);
    }


}