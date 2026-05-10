using System.Security.Cryptography;
using System.Text;
using ShopifyErpSync.Core.Interfaces;

namespace ShopifyErpSync.Api.Middleware;

public class HmacVerificationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _shopifySecret;
    private readonly ILogger<HmacVerificationMiddleware> _logger;

    public HmacVerificationMiddleware(
        RequestDelegate next,
        IConfiguration config,
        ILogger<HmacVerificationMiddleware> logger)
    {
        _next = next;
        _shopifySecret = config["Shopify:WebhookSecret"]
            ?? throw new InvalidOperationException("Missing Shopify:WebhookSecret");
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/shopify"))
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(
            context.Request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        var hmacHeader = context.Request.Headers["X-Shopify-Hmac-Sha256"].ToString();
        if (string.IsNullOrEmpty(hmacHeader))
        {
            _logger.LogWarning("Missing HMAC header on {Path}", context.Request.Path);
            await WriteAuditAsync(context, "WebhookHmacFailed", "Missing HMAC header");
            context.Response.StatusCode = 401;
            return;
        }

        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromBase64String(hmacHeader);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Malformed HMAC header on {Path}", context.Request.Path);
            await WriteAuditAsync(context, "WebhookHmacFailed", "Malformed HMAC header");
            context.Response.StatusCode = 401;
            return;
        }

        var computedHmac = ComputeHmac(body, _shopifySecret);
        var computedBytes = Convert.FromBase64String(computedHmac);

        if (!CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes))
        {
            _logger.LogWarning("Invalid HMAC signature on {Path}", context.Request.Path);
            await WriteAuditAsync(context, "WebhookHmacFailed", "Invalid HMAC signature");
            context.Response.StatusCode = 401;
            return;
        }

        await _next(context);
    }

    private static async Task WriteAuditAsync(HttpContext context, string action, string details)
    {
        try
        {
            var audit = context.RequestServices.GetService<IAuditService>();
            if (audit is not null)
                await audit.LogAsync(action, "Webhook", context.Request.Path, details, false);
        }
        catch { /* audit failure must never block response */ }
    }

    public static string ComputeHmac(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}
