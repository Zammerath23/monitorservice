using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonitorService.Configuration;
using MonitorService.Detection;

namespace MonitorService.Notifications;

public sealed class DiscordWebhookNotifier : INotifier
{
    private const int ColorNew = 0x2ECC71;     // green
    private const int ColorChange = 0xE67E22;  // orange

    private readonly IHttpClientFactory _httpFactory;
    private readonly DiscordOptions _options;
    private readonly ILogger<DiscordWebhookNotifier> _log;

    public DiscordWebhookNotifier(
        IHttpClientFactory httpFactory,
        IOptions<MonitorOptions> options,
        ILogger<DiscordWebhookNotifier> log)
    {
        _httpFactory = httpFactory;
        _options = options.Value.Discord;
        _log = log;
    }

    public async Task NotifyAsync(DetectedChange change, CancellationToken ct)
    {
        if (!_options.Enabled) return;

        var payload = BuildPayload(change);
        var http = _httpFactory.CreateClient("discord");

        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var resp = await http.PostAsJsonAsync(_options.WebhookUrl, payload, ct);

            if (resp.IsSuccessStatusCode) return;

            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = resp.Headers.RetryAfter?.Delta
                                 ?? (resp.Headers.TryGetValues("X-RateLimit-Reset-After", out var v) &&
                                     double.TryParse(v.FirstOrDefault(), System.Globalization.NumberStyles.Any,
                                         System.Globalization.CultureInfo.InvariantCulture, out var sec)
                                     ? TimeSpan.FromSeconds(sec)
                                     : TimeSpan.FromSeconds(2));
                _log.LogWarning("Discord rate-limited, retrying in {Delay}", retryAfter);
                await Task.Delay(retryAfter, ct);
                continue;
            }

            var body = await resp.Content.ReadAsStringAsync(ct);
            _log.LogError("Discord webhook failed: {Status} {Body}", (int)resp.StatusCode, body);
            return;
        }

        _log.LogError("Discord webhook gave up after retries");
    }

    private DiscordWebhookPayload BuildPayload(DetectedChange c)
    {
        var isNew = c.Kind == ChangeKind.NewItem;
        var title = c.Title ?? c.ItemKey;

        var fields = c.Deltas.Select(d => new DiscordEmbedField
        {
            Name = d.Field,
            Value = isNew
                ? Truncate(d.Current ?? "—", 1024)
                : $"~~{Truncate(d.Previous ?? "—", 480)}~~ → **{Truncate(d.Current ?? "—", 480)}**",
            Inline = false
        }).ToArray();

        var embed = new DiscordEmbed
        {
            Title = Truncate(title, 256),
            Url = c.Url,
            Description = isNew ? "🆕 Nueva entrada" : "📈 Cambio detectado",
            Color = isNew ? ColorNew : ColorChange,
            Fields = fields,
            Footer = new DiscordEmbedFooter { Text = c.SourceName },
            Timestamp = c.DetectedAt
        };

        return new DiscordWebhookPayload
        {
            Username = string.IsNullOrWhiteSpace(_options.Username) ? null : _options.Username,
            Embeds = new[] { embed }
        };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}

internal sealed class DiscordWebhookPayload
{
    [JsonPropertyName("username")]   public string? Username { get; set; }
    [JsonPropertyName("content")]    public string? Content { get; set; }
    [JsonPropertyName("embeds")]     public DiscordEmbed[]? Embeds { get; set; }
}

internal sealed class DiscordEmbed
{
    [JsonPropertyName("title")]       public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("url")]         public string? Url { get; set; }
    [JsonPropertyName("color")]       public int? Color { get; set; }
    [JsonPropertyName("fields")]      public DiscordEmbedField[]? Fields { get; set; }
    [JsonPropertyName("footer")]      public DiscordEmbedFooter? Footer { get; set; }
    [JsonPropertyName("timestamp")]   public DateTimeOffset? Timestamp { get; set; }
}

internal sealed class DiscordEmbedField
{
    [JsonPropertyName("name")]   public string Name { get; set; } = "";
    [JsonPropertyName("value")]  public string Value { get; set; } = "";
    [JsonPropertyName("inline")] public bool Inline { get; set; }
}

internal sealed class DiscordEmbedFooter
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
}
