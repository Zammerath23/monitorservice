using System.Net.Http;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using MonitorService.Configuration;

namespace MonitorService.Monitors;

public sealed class HtmlScraperMonitor : IMonitor
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HtmlScraperMonitor> _log;

    public HtmlScraperMonitor(IHttpClientFactory httpFactory, ILogger<HtmlScraperMonitor> log)
    {
        _httpFactory = httpFactory;
        _log = log;
    }

    public SourceType Handles => SourceType.Html;

    public async Task<IReadOnlyList<MonitorItem>> FetchAsync(SourceConfig source, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("scraper");

        using var req = new HttpRequestMessage(HttpMethod.Get, source.Url);
        if (source.Headers is not null)
        {
            foreach (var (k, v) in source.Headers)
                req.Headers.TryAddWithoutValidation(k, v);
        }

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        resp.EnsureSuccessStatusCode();

        var html = await resp.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var watch = source.Watch ?? new Dictionary<string, FieldSelector>();
        var fields = FieldExtractor.ExtractFromHtml(doc, watch);

        var missing = fields.Where(kv => kv.Value is null).Select(kv => kv.Key).ToArray();
        if (missing.Length > 0)
            _log.LogWarning("Source {Source}: selectors returned no match for: {Fields}", source.Name, string.Join(", ", missing));

        var title = fields.TryGetValue("title", out var t) ? t : source.Name;
        var item = new MonitorItem(
            SourceName: source.Name,
            Key: source.Url,
            Fields: fields,
            Url: source.Url,
            Title: title
        );

        return new[] { item };
    }
}
