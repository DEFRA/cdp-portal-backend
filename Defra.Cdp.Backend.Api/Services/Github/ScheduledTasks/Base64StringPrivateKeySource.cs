using System.Text;
using GitHubJwt;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public class Base64StringPrivateKeySource : IPrivateKeySource
{
    private TextReader _keyReader;
    private readonly string _key;

    public Base64StringPrivateKeySource(string encodedPem)
    {
        _key = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPem));
        _keyReader = new StringReader(_key);
    }

    public TextReader GetPrivateKeyReader()
    {
        _keyReader = new StringReader(_key);
        return _keyReader;
    }
}