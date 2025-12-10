using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Teams;
using Defra.Cdp.Backend.Api.Services.Users;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Team = Defra.Cdp.Backend.Api.Services.Teams.Team;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubWorkflowEvents.Services;

public class UsersEventHandlerTest
{

    private readonly IUsersService _usersService = Substitute.For<IUsersService>();
    
    [Fact]
    public async Task Test_handler_accepts_message()
    {

        var handler = new UsersEventHandler(_usersService, new NullLogger<IUsersEventHandler>());

        List<User> users = [new()
        {
            UserId = "cb21f43b-0e8c-4bc2-b782-43fb97c5a8b6",
            Email = "user@email.co",
            Github = "user",
            Name = "User, Name"
        }];
        
        var msg = """
                  {
                    "eventType": "users",
                    "timestamp": "2024-10-23T15:10:10.123",
                    "payload": {
                        "users": [
                            {
                                "user_id": "cb21f43b-0e8c-4bc2-b782-43fb97c5a8b6",
                                "name": "User, Name",
                                "email": "user@email.co",
                                "github": "user"
                                
                            }
                        ]
                    }
                  }
                  """;

        await handler.Handle(msg, TestContext.Current.CancellationToken);
        await _usersService.Received(1).SyncUsers(
            Arg.Is<List<User>>( actualUsers => actualUsers.SequenceEqual(users)), 
            Arg.Any<CancellationToken>());
        
    }
}