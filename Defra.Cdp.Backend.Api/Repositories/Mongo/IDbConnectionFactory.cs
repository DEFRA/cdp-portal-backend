using System.Data;

namespace Defra.Cdp.Backend.Api.Repositories.Mongo;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
}