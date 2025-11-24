using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Defra.Cdp.Backend.Api.Services.Shuttering;

namespace Defra.Cdp.Backend.Api.Tests.Services.Shuttering;

public class ShutteringServiceTest
{
    [Fact]
    public void active_when_request_is_unshuttered_and_actual_is_unshuttered()
    {
        Assert.Equal(ShutteringStatus.Active, ShutteringService.ShutteringStatus(false, false));
    }

    [Fact]
    public void pending_active_when_requested_is_active_and_actual_is_shuttered()
    {
        Assert.Equal(ShutteringStatus.PendingActive, ShutteringService.ShutteringStatus(false, true));
    }

    [Fact]
    public void pending_shuttering_when_request_is_shuttered_and_actual_is_unshuttered()
    {
        Assert.Equal(ShutteringStatus.PendingShuttered, ShutteringService.ShutteringStatus(true, false));
    }

    [Fact]
    public void shuttered_when_request_is_shuttered_and_actual_is_shuttered()
    {
        Assert.Equal(ShutteringStatus.Shuttered, ShutteringService.ShutteringStatus(true, true));
    }

    [Fact]
    public void correctly_detects_waf_type()
    {
        const string urlFrontend = "vanity.url";
        const string urlApi = "apigateway.url";
        
        var tenant = new CdpTenant { 
            Nginx = new CdpTenantNginx { 
                Servers = new Dictionary<string, NginxServer> {
                    { urlFrontend, new NginxServer() }
                } 
            }
        };
        
        Assert.Equal(ShutterUrlType.FrontendVanityUrl, ShutteringService.UrlToWafUrlType(urlFrontend, tenant));
        Assert.Equal(ShutterUrlType.ApiGatewayVanityUrl, ShutteringService.UrlToWafUrlType(urlApi, tenant));
    }
    
}