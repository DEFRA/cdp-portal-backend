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
        var res = validator.Validate(new CreateRuleRequest
        {
            EventType = NotificationTypes.TestFailed,
            Environment = "dev"
        });
        
        Assert.True(res.IsValid);
    }
    
    [Fact]
    public void TestValidInputsNoEnv()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateRuleRequest
        {
            EventType = NotificationTypes.TestPassed
        });
        
        Assert.True(res.IsValid);
    }

    [Fact]
    public void TestInvalidSlackChannel()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateRuleRequest
        {
            EventType = NotificationTypes.TestPassed,
            SlackChannel = " "
        });

        Assert.False(res.IsValid);
    }


}
