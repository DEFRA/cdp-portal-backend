using Defra.Cdp.Backend.Api.Services.Grafana.Models;

namespace Defra.Cdp.Backend.Api.Services.Grafana.Validators;


public static class PromotionEnvironmentValidator
{

    public static List<string> Validate(string environment)
    {
        List<string> errors = [];
        

        if (!PromotionEnvironments.Environments.Contains(environment))
        {
            errors.Add($"Environment is not an environment that can be promoted from: {environment}");
        }
        
        return errors;
    }
}