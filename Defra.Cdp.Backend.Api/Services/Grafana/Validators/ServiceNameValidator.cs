using Defra.Cdp.Backend.Api.Services.Create;

namespace Defra.Cdp.Backend.Api.Services.Grafana.Validators;

public abstract class ServiceNameValidator
{

    public static async Task<List<string>> Validate(string serviceName, IEntityResourceService entities, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        
        // Check service exists
        var serviceExists = await entities.ServiceExists(serviceName, cancellationToken);
        if (!serviceExists)
        {
            errors.Add($"Request contained unknown service: {serviceName}");
        }

        return errors;
    }
}