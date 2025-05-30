using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Tests.Models;

public class UserServiceTests
{
    [Fact]
    public void TestUserResponseDeserialization()
    {
        const string json = """
                            {
                                "message":"success",
                                "user":{
                                    "name":"User, Test",
                                    "email":"test.user@defra.gov.uk",
                                    "github":"test",
                                    "createdAt":"2023-12-13T12:28:09.973Z",
                                    "updatedAt":"2025-02-13T09:58:33.493Z",
                                    "userId":"b474e8e6-5990-4944-8a03-a3bdb054ea43",
                                    "scopes":[],
                                    "teams":[{"teamId":"3b202138-1689-9999-8b55-4227362b249d","name":"Test"}]
                                }
                            }
                            """;

        var output = JsonSerializer.Deserialize<UserServiceUserResponse>(json);
        Assert.NotNull(output);
        Assert.NotNull(output.user);
        Assert.Equivalent(
            new UserServiceUser(
                "User, Test",
                "test.user@defra.gov.uk",
                "b474e8e6-5990-4944-8a03-a3bdb054ea43",
                [new TeamId("3b202138-1689-9999-8b55-4227362b249d", "Test")]
            ),
            output.user);
    }
}