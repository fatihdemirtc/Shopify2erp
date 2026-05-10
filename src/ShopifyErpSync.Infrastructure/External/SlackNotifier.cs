using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopifyErpSync.Core.Interfaces;

namespace ShopifyErpSync.Infrastructure.External;

public class SlackNotifier : INotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _webhookUrl;
    private readonly ILogger<SlackNotifier> _logger;

    public SlackNotifier(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SlackNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _webhookUrl = configuration["Slack:WebhookUrl"];
        _logger = logger;
    }

    public async Task SendSuccessAsync(string message, CancellationToken ct = default)
        => await SendAsync(message, ct);

    public async Task SendFailureAsync(string message, CancellationToken ct = default)
        => await SendAsync(message, ct);

    private async Task SendAsync(string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            _logger.LogInformation("[Slack] {Message}", message);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Slack");
            var response = await client.PostAsJsonAsync(_webhookUrl, new { text = message }, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Slack notification — message: {Message}", message);
        }
    }
}
