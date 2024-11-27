using System.Net;
using System.Text;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.DeploymentTriggers;

public class SelfServiceOpsFetcher
{
   private readonly HttpClient _client;
   private readonly string _baseUrl; 

   public SelfServiceOpsFetcher(IConfiguration configuration, IHttpClientFactory httpClientFactory)
   {
      _baseUrl = configuration.GetValue<string>("SelfServiceOpsUrl")!;
      if (string.IsNullOrWhiteSpace(_baseUrl))
         throw new ArgumentNullException("selfServiceOpsBackendUrl", "Self service ops backend url cannot be null");
      _client = httpClientFactory.CreateClient("DefaultClient");
   }

   public async Task<HttpStatusCode> TriggerTestSuite(string imageName, string environment, UserDetails? user, CancellationToken cancellationToken)
   {
      var body = new
      {
          imageName,
          environment,
          user
      };
      var payload = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
      var result = await _client.PostAsync(_baseUrl + "/trigger-test-suite", payload, cancellationToken);
      result.EnsureSuccessStatusCode();
      return result.StatusCode;
   }
}
