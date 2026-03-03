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
            Environments = ["dev"]
        });
        
        Assert.True(res.IsValid);
    }
 
    [Fact]
    public void TestValidInputsWrongEnv()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateRuleRequest
        {
            EventType = NotificationTypes.TestPassed,
            Environments = ["dev", "test", "foo"]
        });
        
        Assert.False(res.IsValid);
    }

    [Fact]
    public void TestInvalidSlackChannel()
    {
        var validator = new CreateNotificationRuleRequestValidator();
        var res = validator.Validate(new CreateRuleRequest
        {
            EventType = NotificationTypes.TestPassed,
            Environments = ["dev"],
            SlackChannel = " "
        });

        Assert.False(res.IsValid);
    }


}
