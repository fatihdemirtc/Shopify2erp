using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace ShopifyErpSync.Infrastructure.Adapters.Odoo;

// Odoo XML-RPC client — stateless per call, API key auth, no session management
public class OdooClient
{
    private readonly HttpClient _http;
    private readonly OdooSettings _settings;
    private readonly ILogger<OdooClient> _logger;
    private int? _uid;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    public OdooClient(HttpClient http, OdooSettings settings, ILogger<OdooClient> logger)
    {
        _http = http;
        _settings = settings;
        _logger = logger;
    }

    private async Task<int> GetUidAsync(CancellationToken ct)
    {
        if (_uid.HasValue) return _uid.Value;
        await _authLock.WaitAsync(ct);
        try
        {
            if (_uid.HasValue) return _uid.Value;
            var xml = BuildCall("authenticate",
                _settings.Database, _settings.Username, _settings.ApiKey,
                new Dictionary<string, object?>());
            var result = await PostXmlRpcAsync("/xmlrpc/2/common", xml, ct);
            _uid = Convert.ToInt32(result ?? throw new InvalidOperationException("Odoo auth returned null/false — check credentials"));
            _logger.LogInformation("Odoo auth OK — uid={Uid} ({BaseUrl})", _uid, _settings.BaseUrl);
            return _uid.Value;
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async Task<List<Dictionary<string, object?>>> SearchReadAsync(
        string model, object[] domain, string[] fields, int limit = 100, CancellationToken ct = default)
    {
        var result = await ExecuteKwAsync(model, "search_read",
            args: new object[] { domain },
            kwargs: new { fields, limit },
            ct: ct);

        if (result is not List<object?> list)
            throw new InvalidOperationException($"search_read '{model}' unexpected result type: {result?.GetType()}");

        return list.Cast<Dictionary<string, object?>>().ToList();
    }

    public async Task<int> CreateAsync(string model, object values, CancellationToken ct = default)
    {
        var result = await ExecuteKwAsync(model, "create",
            args: new object[] { values },
            kwargs: new { },
            ct: ct);
        return Convert.ToInt32(result ?? throw new InvalidOperationException($"create '{model}' returned null"));
    }

    public async Task WriteAsync(string model, int id, object values, CancellationToken ct = default)
    {
        await ExecuteKwAsync(model, "write",
            args: new object[] { new int[] { id }, values },
            kwargs: new { },
            ct: ct);
    }

    public async Task ExecuteAsync(string model, int id, string method, CancellationToken ct = default)
    {
        await ExecuteKwAsync(model, method,
            args: new object[] { new int[] { id } },
            kwargs: new { },
            ct: ct);
        _logger.LogDebug("Odoo {Method} on {Model}/{Id} OK", method, model, id);
    }

    private async Task<object?> ExecuteKwAsync(
        string model, string method, object args, object kwargs, CancellationToken ct)
    {
        var uid = await GetUidAsync(ct);
        var xml = BuildCall("execute_kw",
            _settings.Database, uid, _settings.ApiKey, model, method, args, kwargs);
        return await PostXmlRpcAsync("/xmlrpc/2/object", xml, ct);
    }

    private async Task<object?> PostXmlRpcAsync(string endpoint, string xml, CancellationToken ct)
    {
        using var content = new StringContent(xml, Encoding.UTF8, "text/xml");
        var resp = await _http.PostAsync(endpoint, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Odoo {endpoint} HTTP {(int)resp.StatusCode}: {body}");
        return ParseResponse(body);
    }

    // ── XML-RPC serializer ──────────────────────────────────────────────────

    private static string BuildCall(string methodName, params object?[] parameters)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\"?><methodCall><methodName>");
        sb.Append(SecurityElement.Escape(methodName));
        sb.Append("</methodName><params>");
        foreach (var p in parameters)
        {
            sb.Append("<param><value>");
            SerializeValue(sb, p);
            sb.Append("</value></param>");
        }
        sb.Append("</params></methodCall>");
        return sb.ToString();
    }

    private static void SerializeValue(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append("<nil/>"); break;
            case bool b:
                sb.Append(b ? "<boolean>1</boolean>" : "<boolean>0</boolean>"); break;
            case int i:
                sb.Append($"<int>{i}</int>"); break;
            case long l:
                sb.Append($"<int>{l}</int>"); break;
            case double d:
                sb.Append($"<double>{d.ToString(CultureInfo.InvariantCulture)}</double>"); break;
            case float f:
                sb.Append($"<double>{f.ToString(CultureInfo.InvariantCulture)}</double>"); break;
            case string s:
                sb.Append("<string>"); sb.Append(SecurityElement.Escape(s)); sb.Append("</string>"); break;
            case int[] intArr:
                SerializeSequence(sb, intArr.Cast<object?>().ToArray()); break;
            case object[] arr:
                SerializeSequence(sb, arr); break;
            case List<object> list:
                SerializeSequence(sb, [.. list]); break;
            case Dictionary<string, object?> dict:
                SerializeStruct(sb, dict); break;
            default:
                // Anonymous objects and other complex types → via JSON
                SerializeJsonElement(sb, JsonSerializer.SerializeToElement(value));
                break;
        }
    }

    private static void SerializeSequence(StringBuilder sb, object?[] items)
    {
        sb.Append("<array><data>");
        foreach (var item in items)
        {
            sb.Append("<value>"); SerializeValue(sb, item); sb.Append("</value>");
        }
        sb.Append("</data></array>");
    }

    private static void SerializeStruct(StringBuilder sb, Dictionary<string, object?> dict)
    {
        sb.Append("<struct>");
        foreach (var (key, val) in dict)
        {
            sb.Append("<member><name>"); sb.Append(SecurityElement.Escape(key));
            sb.Append("</name><value>"); SerializeValue(sb, val); sb.Append("</value></member>");
        }
        sb.Append("</struct>");
    }

    private static void SerializeJsonElement(StringBuilder sb, JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
                sb.Append("<nil/>"); break;
            case JsonValueKind.True:
                sb.Append("<boolean>1</boolean>"); break;
            case JsonValueKind.False:
                sb.Append("<boolean>0</boolean>"); break;
            case JsonValueKind.String:
                sb.Append("<string>"); sb.Append(SecurityElement.Escape(el.GetString()!)); sb.Append("</string>"); break;
            case JsonValueKind.Number:
                if (el.TryGetInt64(out var l)) sb.Append($"<int>{l}</int>");
                else sb.Append($"<double>{el.GetDouble().ToString(CultureInfo.InvariantCulture)}</double>");
                break;
            case JsonValueKind.Array:
                sb.Append("<array><data>");
                foreach (var item in el.EnumerateArray())
                {
                    sb.Append("<value>"); SerializeJsonElement(sb, item); sb.Append("</value>");
                }
                sb.Append("</data></array>");
                break;
            case JsonValueKind.Object:
                sb.Append("<struct>");
                foreach (var prop in el.EnumerateObject())
                {
                    sb.Append("<member><name>"); sb.Append(SecurityElement.Escape(prop.Name));
                    sb.Append("</name><value>"); SerializeJsonElement(sb, prop.Value); sb.Append("</value></member>");
                }
                sb.Append("</struct>");
                break;
        }
    }

    // ── XML-RPC response parser ─────────────────────────────────────────────

    private static object? ParseResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var fault = doc.Root?.Element("fault");
        if (fault != null)
        {
            var data = ParseValue(fault.Element("value")!) as Dictionary<string, object?>;
            var msg = data?["faultString"]?.ToString() ?? "Unknown fault";
            throw new InvalidOperationException($"Odoo fault: {msg}");
        }
        var param = doc.Root?.Element("params")?.Element("param")?.Element("value");
        return param is not null ? ParseValue(param) : null;
    }

    private static object? ParseValue(XElement valueEl)
    {
        var child = valueEl.Elements().FirstOrDefault();
        if (child is null) return valueEl.Value;

        return child.Name.LocalName switch
        {
            "int" or "i4" or "i8" =>
                int.TryParse(child.Value, out var i) ? i : 0,
            "boolean" =>
                child.Value == "1",
            "string" =>
                child.Value,
            "double" =>
                double.TryParse(child.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0,
            "nil" =>
                null,
            "array" =>
                (object?)(child.Element("data")?.Elements("value").Select(ParseValue).ToList()
                          ?? new List<object?>()),
            "struct" =>
                child.Elements("member").ToDictionary(
                    m => m.Element("name")!.Value,
                    m => ParseValue(m.Element("value")!)),
            _ =>
                child.Value
        };
    }
}
