using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace MonitorService.Monitors;

public sealed class PlaywrightBrowserAccessor : IAsyncDisposable
{
    private readonly ILogger<PlaywrightBrowserAccessor> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightBrowserAccessor(ILogger<PlaywrightBrowserAccessor> log) => _log = log;

    public async Task<IBrowser> GetAsync(CancellationToken ct)
    {
        if (_browser is { IsConnected: true }) return _browser;

        await _gate.WaitAsync(ct);
        try
        {
            if (_browser is { IsConnected: true }) return _browser;

            EnsureBrowsersInstalled();

            _playwright ??= await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
            return _browser;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureBrowsersInstalled()
    {
        try
        {
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
                _log.LogWarning("Playwright browser install returned exit code {Code}", exitCode);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Playwright browser install failed; will try to launch anyway");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _gate.Dispose();
    }
}
