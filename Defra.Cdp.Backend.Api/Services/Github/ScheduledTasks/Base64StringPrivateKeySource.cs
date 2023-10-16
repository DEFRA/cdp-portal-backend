using System.Text;
using GitHubJwt;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public class Base64StringPrivateKeySource : IPrivateKeySource
{
    private readonly TextReader _keyReader;

    public Base64StringPrivateKeySource(string encodedPem)
    {
        var key = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPem));
        _keyReader = new StringReader(key);
    }

    public TextReader GetPrivateKeyReader()
    {
        return _keyReader;
    }
}