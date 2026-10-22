namespace Hookline.Application.Services
{
    public interface IWebhookDeliveryService
    {
        Task<(bool IsSuccess, int? StatusCode, string? Error)> DeliveryAsync(string url, string payload, CancellationToken ct);
    }   
}
