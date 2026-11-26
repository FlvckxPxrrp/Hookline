using Hookline.Application.Services;
using System.Reflection.Metadata.Ecma335;
using System.Security.AccessControl;
using System.Text;

namespace Hookline.Infrastructure.Services
{
    public class WebhookDeliveryService : IWebhookDeliveryService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public WebhookDeliveryService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<(bool IsSuccess, int? StatusCode, string? Error)> DeliveryAsync
            (string url, string payload, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("WebhookDeliveryClient");

                using var content = new StringContent(
                    payload,
                    Encoding.UTF8,
                    "application/json");

                using var response = await client.PostAsync(url, content, ct);

                if(response.IsSuccessStatusCode)
                    return (true,(int)response.StatusCode, null);

                return (
                    false,
                    (int)response.StatusCode,
                    $"HttpError{response.StatusCode}");
            }            
            catch(OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch(OperationCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                return (false, null, "Timeout");
            }            
            catch(HttpRequestException ex)
            {
                return (false, null, ex.Message);
            }
        }
    }
}
