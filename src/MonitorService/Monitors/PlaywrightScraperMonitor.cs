using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using MonitorService.Configuration;

namespace MonitorService.Monitors;

public sealed class PlaywrightScraperMonitor : IMonitor
{
    private readonly PlaywrightBrowserAccessor _accessor;
    private readonly ILogger<PlaywrightScraperMonitor> _log;

    public PlaywrightScraperMonitor(PlaywrightBrowserAccessor accessor, ILogger<PlaywrightScraperMonitor> log)
    {
        _accessor = accessor;
        _log = log;
    }

    public SourceType Handles => SourceType.Playwright;

    public async Task<IReadOnlyList<MonitorItem>> FetchAsync(SourceConfig source, CancellationToken ct)
    {
        var browser = await _accessor.GetAsync(ct);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                        "(KHTML, like Gecko) Chrome/130.0 Safari/537.36",
            ExtraHTTPHeaders = source.Headers
        });

        try
        {
            var page = await context.NewPageAsync();
            await page.GotoAsync(source.Url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 45_000
            });

            if (!string.IsNullOrWhiteSpace(source.WaitFor))
            {
                try
                {
                    await page.WaitForSelectorAsync(source.WaitFor, new PageWaitForSelectorOptions
                    {
                        Timeout = 20_000,
                        // Attached (default would be Visible) — supports <meta>, hidden inputs and
                        // other non-rendered elements that still expose data via attributes.
                        State = WaitForSelectorState.Attached
                    });
                }
                catch (TimeoutException)
                {
                    _log.LogWarning("Source {Source}: waitFor selector '{Sel}' not found within timeout", source.Name, source.WaitFor);
                }
            }

            var html = await page.ContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var watch = source.Watch ?? new Dictionary<string, FieldSelector>();
            var fields = FieldExtractor.ExtractFromHtml(doc, watch);

            var missing = fields.Where(kv => kv.Value is null).Select(kv => kv.Key).ToArray();
            if (missing.Length > 0)
                _log.LogWarning("Source {Source}: selectors returned no match for: {Fields}", source.Name, string.Join(", ", missing));

            var title = fields.TryGetValue("title", out var t) ? t : source.Name;
            return new[]
            {
                new MonitorItem(
                    SourceName: source.Name,
                    Key: source.Url,
                    Fields: fields,
                    Url: source.Url,
                    Title: title
                )
            };
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
