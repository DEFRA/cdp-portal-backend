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
                                "name":"User, Test",
                                "email":"test.user@defra.gov.uk",
                                "github":"test",
                                "createdAt":"2023-12-13T12:28:09.973Z",
                                "updatedAt":"2025-02-13T09:58:33.493Z",
                                "userId":"b474e8e6-5990-4944-8a03-a3bdb054ea43",
                                "scopes":[],
                                "teams":[{"teamId":"test","name":"Test"}]
                            }
                        """;

        var output = JsonSerializer.Deserialize<UserServiceUser>(json);
        Assert.NotNull(output);
        Assert.Equivalent(
            new UserServiceUser(
                "User, Test",
                "test.user@defra.gov.uk",
                "b474e8e6-5990-4944-8a03-a3bdb054ea43",
                [new Team("test", "Test")]
            ),
            output);
    }
}