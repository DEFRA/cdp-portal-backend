using System.Net;
using System.Text;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Services.DeploymentTriggers;

public class SelfServiceOpsFetcher
{
   private readonly HttpClient _client;

   public SelfServiceOpsFetcher(IConfiguration configuration)
   {
      var selfServiceOpsUrl = configuration.GetValue<string>("SelfServiceOpsUrl")!;
      if (string.IsNullOrWhiteSpace(selfServiceOpsUrl))
         throw new ArgumentNullException("selfServiceOpsBackendUrl", "Self service ops backend url cannot be null");
      _client = new HttpClient();
      _client.BaseAddress = new Uri(selfServiceOpsUrl);
      _client.DefaultRequestHeaders.Accept.Clear();
      _client.DefaultRequestHeaders.Add(
          "Accept", "application/json");
      _client.DefaultRequestHeaders.Add("User-Agent",
          "cdp-portal-backend");
   }

   public async Task<HttpStatusCode> triggerTestSuite(string ImageName, string Environment, CancellationToken cancellationToken)
   {
      var body = new
      {
         imageName = ImageName,
         environment = Environment
      };
      var payload = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
      var result = await _client.PostAsync("/trigger-test-suite", payload, cancellationToken);
      result.EnsureSuccessStatusCode();
      return result.StatusCode;
   }
}
