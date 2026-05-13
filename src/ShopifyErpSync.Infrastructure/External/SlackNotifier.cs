using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShopifyErpSync.Core.Interfaces;

namespace ShopifyErpSync.Infrastructure.External;

public class SlackNotifier : INotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _webhookUrl;
    private readonly ILogger<SlackNotifier> _logger;

    // matches: "✅ Order #1015 synced — $629.95 — John Doe"
    private static readonly Regex OrderPattern =
        new(@"Order #(\S+) synced — \$([0-9.,]+) — (.+)$", RegexOptions.Compiled);

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
        => await PostAsync(BuildSuccessPayload(message), message, ct);

    public async Task SendFailureAsync(string message, CancellationToken ct = default)
        => await PostAsync(BuildFailurePayload(message), message, ct);

    private static object BuildSuccessPayload(string message)
    {
        var ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        var m = OrderPattern.Match(message);

        if (m.Success)
        {
            return new
            {
                attachments = new[]
                {
                    new
                    {
                        color = "#10b981",
                        fallback = message,
                        blocks = new object[]
                        {
                            new
                            {
                                type = "section",
                                text = new { type = "mrkdwn", text = "*:white_check_mark:  Order Synced to ERP*" }
                            },
                            new
                            {
                                type = "section",
                                fields = new[]
                                {
                                    new { type = "mrkdwn", text = $"*Order*\n#{m.Groups[1].Value}" },
                                    new { type = "mrkdwn", text = $"*Amount*\n${m.Groups[2].Value}" },
                                    new { type = "mrkdwn", text = $"*Customer*\n{m.Groups[3].Value}" }
                                }
                            },
                            new
                            {
                                type = "context",
                                elements = new[] { new { type = "mrkdwn", text = $":clock3:  {ts}" } }
                            }
                        }
                    }
                }
            };
        }

        return new
        {
            attachments = new[]
            {
                new
                {
                    color = "#10b981",
                    fallback = message,
                    blocks = new object[]
                    {
                        new
                        {
                            type = "section",
                            text = new { type = "mrkdwn", text = $"*:white_check_mark:  {message}*" }
                        },
                        new
                        {
                            type = "context",
                            elements = new[] { new { type = "mrkdwn", text = $":clock3:  {ts}" } }
                        }
                    }
                }
            }
        };
    }

    private static object BuildFailurePayload(string message)
    {
        var ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        return new
        {
            attachments = new[]
            {
                new
                {
                    color = "#ef4444",
                    fallback = message,
                    blocks = new object[]
                    {
                        new
                        {
                            type = "section",
                            text = new { type = "mrkdwn", text = $"*:x:  Order Sync Failed*\n```{message}```" }
                        },
                        new
                        {
                            type = "context",
                            elements = new[] { new { type = "mrkdwn", text = $":clock3:  {ts}" } }
                        }
                    }
                }
            }
        };
    }

    private async Task PostAsync(object payload, string fallback, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            _logger.LogInformation("[Slack] {Message}", fallback);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Slack");
            var response = await client.PostAsJsonAsync(_webhookUrl, payload, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Slack notification — message: {Message}", fallback);
        }
    }
}
