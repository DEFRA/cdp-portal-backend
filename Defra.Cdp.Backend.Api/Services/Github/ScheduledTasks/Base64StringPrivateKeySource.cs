using System.Text;
using GitHubJwt;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public class Base64StringPrivateKeySource(string encodedPem) : IPrivateKeySource
{
    private readonly string _key = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPem));

    public TextReader GetPrivateKeyReader()
    {
        return new StringReader(_key);
    }
}