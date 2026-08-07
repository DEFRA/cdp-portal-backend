using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

namespace Defra.Cdp.Backend.Api.Tests.Services.Entities;

public class TopologyServiceCombinerTests
{   
    [Fact]
    public void Combine_handles_null_resource_lists()
    {
        var primary = new List<TopologyService>();
        var secondary = new List<TopologyService>();

        var resources = TopologyServiceCombiner.Combine(primary, secondary);

        Assert.Empty(resources);
    }

    [Fact]
    public void Combine_combines_existing_with_new_services()
    {
        var testTeam = new Team() {
            TeamId = "test",
            Name = "Test"
        };

        var primary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", [])
            ])
        ]);

        var secondary = new List<TopologyService>([
            new TopologyService("new-service", SubType.Backend, [testTeam], [
                new TopologyResource("new-resource", "type", "icon", []){
                    ResourceRequestId = "123"
                }
            ]),
        ]);

        var relationships = TopologyServiceCombiner.Combine(primary, secondary);

        var expected = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", [])
            ]),
            new TopologyService("new-service", SubType.Backend, [testTeam], [
                new TopologyResource("new-resource", "type", "icon", []){
                    ResourceRequestId = "123"
                }
            ]),
        ]);
        
        Assert.Equivalent(relationships, expected, true);
    }

    [Fact]
    public void Combine_combines_existing_with_new_resources()
    {
        var testTeam = new Team()
        {
            TeamId = "test",
            Name = "Test"
        };

        var primary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", [])
            ])
        ]);

        var secondary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("new-resource", "type", "icon", []){
                    ResourceRequestId = "123"
                }
            ])
        ]);

        var relationships = TopologyServiceCombiner.Combine(primary, secondary);

        var expected = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", []),
                new TopologyResource("new-resource", "type", "icon", []){
                    ResourceRequestId = "123"
                }
            ])
        ]);

        Assert.Equivalent(relationships, expected, true);
    }

    [Fact]
    public void Combine_combines_existing_with_existing_resource()
    {
        var testTeam = new Team()
        {
            TeamId = "test",
            Name = "Test"
        };

        var primary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", [])
            ])
        ]);

        var secondary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", []){
                    ResourceRequestId = "123"
                }
            ])
        ]);

        var relationships = TopologyServiceCombiner.Combine(primary, secondary);

        var expected = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", [])
            ])
        ]);

        Assert.Equivalent(relationships, expected, true);
    }

    [Fact]
    public void Combine_combines_existing_with_new_resource_with_link_to_new_service()
    {
        var testTeam = new Team()
        {
            TeamId = "test",
            Name = "Test"
        };

        var primary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", [])
            ])
        ]);

        var secondary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("new-resource", "type", "icon", []){
                    ResourceRequestId = "123"
                }
            ]),
            new TopologyService("new-service", SubType.Frontend, [testTeam], [
                new TopologyResource("another-resource", EntityResourceMapper.SNS.Name, EntityResourceMapper.SNS.Icon, [
                    new TopologyResourceLink("existing-service", "existing-resource", "type", "subscription" ){
                        ResourceRequestId = "123"
                    }               
                ]){
                    ResourceRequestId = "123"
                }
            ])
        ]);

        var relationships = TopologyServiceCombiner.Combine(primary, secondary);

        var expected = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-resource", "type", "icon", []),
                new TopologyResource("new-resource", "type", "icon", []){
                    ResourceRequestId = "123"
                }
            ]),
            new TopologyService("new-service", SubType.Frontend, [testTeam], [
                new TopologyResource("another-resource", EntityResourceMapper.SNS.Name, EntityResourceMapper.SNS.Icon, [
                    new TopologyResourceLink("existing-service", "existing-resource", "type", "subscription" ){
                        ResourceRequestId = "123"
                    } 
                ]){
                    ResourceRequestId = "123"
                }
            ])
        ]);

        Assert.Equivalent(relationships, expected, true);
    }

    [Fact]
    public void Combine_combines_existing_with_new_link()
    {
        var testTeam = new Team() {
            TeamId = "test",
            Name = "Test"
        };

        var primary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-queue", "queue", "icon", []),
                new TopologyResource("existing-topic", "topic", "icon", [])        
            ])
        ]);

        var secondary = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-queue", "queue", "icon", [
                    new TopologyResourceLink("existing-service", "existing-topic", "type", "subscription"){
                        ResourceRequestId = "123"
                    }
                ]){
                    ResourceRequestId = "123"
                }
            ])
        ]);

        var relationships = TopologyServiceCombiner.Combine(primary, secondary);

        var expected = new List<TopologyService>([
            new TopologyService("existing-service", SubType.Backend, [testTeam], [
                new TopologyResource("existing-queue", "queue", "icon", [
                    new TopologyResourceLink("existing-service", "existing-topic", "type", "subscription"){
                        ResourceRequestId = "123"
                    }
                ]),
                new TopologyResource("existing-topic", "topic", "icon", [])
            ])
        ]);
        
        Assert.Equivalent(relationships, expected, true);
    }
}