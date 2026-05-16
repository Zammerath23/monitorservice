using System.Net.Http;
using Microsoft.Playwright;
using MonitorService.Configuration;
using MonitorService.Monitors;

namespace MonitorService.Diagnostics;

/// <summary>
/// Fetches a single source and writes the raw payload (HTML for html/playwright, XML for rss)
/// to disk under DataDirectory\dumps. Used by the --dump-html CLI to help users discover
/// the right CSS selectors without keeping the worker loop running.
/// </summary>
public sealed class SourceDumper
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly PlaywrightBrowserAccessor _playwrightAccessor;

    public SourceDumper(IHttpClientFactory httpFactory, PlaywrightBrowserAccessor playwrightAccessor)
    {
        _httpFactory = httpFactory;
        _playwrightAccessor = playwrightAccessor;
    }

    public async Task<string> DumpAsync(SourceConfig source, string dumpsDir, CancellationToken ct)
    {
        Directory.CreateDirectory(dumpsDir);
        var ext = source.Type == SourceType.Rss ? "xml" : "html";
        var safeName = string.Join("_", source.Name.Split(Path.GetInvalidFileNameChars()));
        var outPath = Path.Combine(dumpsDir, $"{safeName}.{ext}");

        var payload = source.Type switch
        {
            SourceType.Rss        => await FetchHttpAsync(source, ct),
            SourceType.Html       => await FetchHttpAsync(source, ct),
            SourceType.Playwright => await FetchPlaywrightAsync(source, ct),
            _                     => throw new NotSupportedException($"Unknown type {source.Type}")
        };

        await File.WriteAllTextAsync(outPath, payload, ct);
        return outPath;
    }

    private async Task<string> FetchHttpAsync(SourceConfig source, CancellationToken ct)
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
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private async Task<string> FetchPlaywrightAsync(SourceConfig source, CancellationToken ct)
    {
        var browser = await _playwrightAccessor.GetAsync(ct);
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
                        State = WaitForSelectorState.Attached
                    });
                }
                catch (TimeoutException) { /* dump anyway, the warning helps */ }
            }

            return await page.ContentAsync();
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
