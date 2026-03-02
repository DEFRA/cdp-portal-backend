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
            EventType = NotificationTypes.TestFailed,
            Environment = "dev"
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
            EventType = NotificationTypes.TestPassed
        });
        
        Assert.True(res.IsValid);
    }
    
    [Fact]
    public void TestInvalidInputsNoEnv()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateNotificationRuleRequest
        {
            Entity = null!,
            EventType = NotificationTypes.TestPassed
        });
        
        Assert.False(res.IsValid);
    }

    [Fact]
    public void TestInvalidSlackChannel()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateNotificationRuleRequest
        {
            Entity = "foo",
            EventType = NotificationTypes.TestPassed,
            SlackChannel = " "
        });

        Assert.False(res.IsValid);
    }


}
