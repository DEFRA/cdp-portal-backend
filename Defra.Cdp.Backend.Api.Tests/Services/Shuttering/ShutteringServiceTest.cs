using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Shuttering;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Tests.Services.Shuttering;

public class ShutteringServiceTest
{
    [Fact]
    public void ShutteringStatusIsShutteredWhenVanityUrlIsShutteredAndNoMatchingShutteringRecord()
    {
        VanityUrlRecord vanity = new VanityUrlRecord("https://test-url.com", "test-env", "test-service", false, true);
        var shutteringStatus = ShutteringService.ShutteringStatus(vanity.Shuttered, null);
        
        Assert.Equal(ShutteringStatus.Shuttered, shutteringStatus);
    }
    
    [Fact]
    public void ShutteringStatusIsActiveWhenVanityUrlIsNotShutteredAndNoMatchingShutteringRecord()
    {
        VanityUrlRecord vanity = new VanityUrlRecord("https://test-url.com", "test-env", "test-service", false, false);
        var shutteringStatus = ShutteringService.ShutteringStatus(vanity.Shuttered, null);
        
        Assert.Equal(ShutteringStatus.Active, shutteringStatus);
    }
    
    [Fact]
    public void ShutteringStatusIsShutteredWhenVanityUrlIsShutteredAndShutteringRecordIsShuttered()
    {
        VanityUrlRecord vanity = new VanityUrlRecord("https://test-url.com", "test-env", "test-service", false, true);
        var shutteringStatus = ShutteringService.ShutteringStatus(vanity.Shuttered, new ShutteringRecord("test-env", "test-service", "https://test-url.com", "waf", true,
            new User { Id = "9999-9999-9999", DisplayName = "Test User" },
            DateTime.UtcNow));
        
        Assert.Equal(ShutteringStatus.Shuttered, shutteringStatus);
    }

    
    [Fact]
    public void ShutteringStatusIsPendingActiveWhenVanityUrlIsShutteredAndShutteringRecordIsNotShuttered()
    {
        VanityUrlRecord vanity = new VanityUrlRecord("https://test-url.com", "test-env", "test-service", false, true);
        var shutteringStatus = ShutteringService.ShutteringStatus(vanity.Shuttered, new ShutteringRecord("test-env", "test-service", "https://test-url.com", "waf", false,
            new User { Id = "9999-9999-9999", DisplayName = "Test User" }, DateTime.UtcNow));
        
        Assert.Equal(ShutteringStatus.PendingActive, shutteringStatus);
    }
    
    [Fact]
    public void ShutteringStatusIsActiveWhenVanityUrlIsNotShutteredAndShutteringRecordIsNotShuttered()
    {
        VanityUrlRecord vanity = new VanityUrlRecord("https://test-url.com", "test-env", "test-service", false, false);
        var shutteringStatus = ShutteringService.ShutteringStatus(vanity.Shuttered, new ShutteringRecord("test-env", "test-service", "https://test-url.com", "waf", false,
            new User { Id = "9999-9999-9999", DisplayName = "Test User" }, DateTime.UtcNow));
        
        Assert.Equal(ShutteringStatus.Active, shutteringStatus);
    }
    
    [Fact]
    public void ShutteringStatusIsPendingShutteredWhenVanityUrlIsNotShutteredAndShutteringRecordIsShuttered()
    {
        VanityUrlRecord vanity = new VanityUrlRecord("https://test-url.com", "test-env", "test-service", false, false);
        var shutteringStatus = ShutteringService.ShutteringStatus(vanity.Shuttered, new ShutteringRecord("test-env", "test-service", "https://test-url.com", "waf", true,
            new User { Id = "9999-9999-9999", DisplayName = "Test User" }, DateTime.UtcNow));
        
        Assert.Equal(ShutteringStatus.PendingShuttered, shutteringStatus);
    }

}