namespace ShopifyErpSync.Core.Interfaces;

public interface INotificationService
{
    Task SendSuccessAsync(string message, CancellationToken ct = default);
    Task SendFailureAsync(string message, CancellationToken ct = default);
}
