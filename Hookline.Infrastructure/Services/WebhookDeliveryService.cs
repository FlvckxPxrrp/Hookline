using Hookline.Application.Services;
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
                var response = await client.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"), ct);

                if (response.IsSuccessStatusCode)
                {
                    return (true, (int)response.StatusCode, null);
                }

                return (false, (int)response.StatusCode, $"HttpError {response.StatusCode}");
            }
            catch (TaskCanceledException e) when (e.InnerException is TimeoutException)
            {
                return (false, null, "TimeOut");
            }

            catch(HttpRequestException ex)
            {
                return(false, null, ex.Message);
            }

            catch(Exception ex)
            {
                return (false, null, ex.Message);
            }
        }
    }
}
