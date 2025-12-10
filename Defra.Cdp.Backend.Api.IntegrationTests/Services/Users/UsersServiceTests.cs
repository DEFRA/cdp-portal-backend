using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Users;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Users;

public class UsersServiceTests(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{

    [Fact]
    public async Task Test_CRUD_functions()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var usersService = new UsersService(connectionFactory, new NullLoggerFactory());

        var user = new User
        {
            UserId = "cb21f43b-0e8c-4bc2-b782-43fb97c5a8b6",
            Email = "user@email.co",
            Github = "ghuser",
            Name = "User, Name"
        };

        {
            var userBeforeCreate = await usersService.Find(user.UserId, TestContext.Current.CancellationToken);
            Assert.Null(userBeforeCreate);
        }

        // Create
        {
            await usersService.CreateUser(user, TestContext.Current.CancellationToken);
            var userAfterCreate = await usersService.Find(user.UserId, TestContext.Current.CancellationToken);
            Assert.Equivalent(user, userAfterCreate);
        }
        
        // Update 
        {
            var updatedUser = user with { Name = "Updated Name" };
            await usersService.UpdateUser(updatedUser, TestContext.Current.CancellationToken);
            var userAfterUpdate = await usersService.Find(user.UserId, TestContext.Current.CancellationToken);
            Assert.Equivalent(updatedUser, userAfterUpdate);
        }
        
        // Delete 
        {
            await usersService.DeleteUser(user.UserId, TestContext.Current.CancellationToken);
            var userAfterDelete = await usersService.Find(user.UserId, TestContext.Current.CancellationToken);
            Assert.Null(userAfterDelete);
        }
    }
    
    [Fact]
    public async Task test_sync_users_works()
    {
        var connectionFactory = CreateMongoDbClientFactory();
        var usersService = new UsersService(connectionFactory, new NullLoggerFactory());

        var user1 = new User
        {
            UserId = "cb21f43b-0e8c-4bc2-b782-43fb97c5a8b6",
            Email = "user@email.co",
            Github = "ghuser",
            Name = "User, Name"
        };
        
        var user2 = new User
        {
            UserId = "ddc8e95c-183e-4683-a332-335b59423528",
            Email = "bob@email.co",
            Github = "ghbob",
            Name = "Bob, Rob"
        };
        
        List<User> users = [ user1, user2 ];

        // Initial sync
        {
            await usersService.SyncUsers(users, TestContext.Current.CancellationToken);
            var usersAfterSync = await usersService.FindAll(TestContext.Current.CancellationToken);
            Assert.Equal(2, usersAfterSync.Count);
            Assert.Equivalent(users, usersAfterSync);
        }
        
        // Remove user via sync
        {
            await usersService.SyncUsers([user1], TestContext.Current.CancellationToken);
            var usersAfterRemovalSync = await usersService.FindAll(TestContext.Current.CancellationToken);
            Assert.Single(usersAfterRemovalSync);
            Assert.Equivalent(user1, usersAfterRemovalSync[0]);
        }
        
        // Empty user payloads should fail
        {
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await usersService.SyncUsers([], TestContext.Current.CancellationToken));
        }
        
    }
}