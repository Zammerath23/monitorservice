using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MonitorService.Configuration;
using MonitorService.Detection;
using MonitorService.Monitors;
using MonitorService.Notifications;
using MonitorService.Persistence;

namespace MonitorService;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _log;
    private readonly MonitorOptions _options;
    private readonly SourcesLoader _loader;
    private readonly IReadOnlyDictionary<SourceType, IMonitor> _monitors;
    private readonly ChangeDetector _detector;
    private readonly INotifier _notifier;
    private readonly ISnapshotRepository _repo;

    public Worker(
        ILogger<Worker> log,
        IOptions<MonitorOptions> options,
        SourcesLoader loader,
        IEnumerable<IMonitor> monitors,
        ChangeDetector detector,
        INotifier notifier,
        ISnapshotRepository repo)
    {
        _log = log;
        _options = options.Value;
        _loader = loader;
        _monitors = monitors.ToDictionary(m => m.Handles);
        _detector = detector;
        _notifier = notifier;
        _repo = repo;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _repo.InitializeAsync(stoppingToken);

        var sourcesPath = Path.GetFullPath(_options.SourcesFile);
        _log.LogInformation("Loading sources from {Path}", sourcesPath);

        SourcesFile file;
        try
        {
            file = _loader.Load(sourcesPath);
        }
        catch (Exception ex)
        {
            _log.LogCritical(ex, "Cannot load sources file; exiting");
            return;
        }

        var enabled = file.Sources.Where(s => s.Enabled).ToArray();
        _log.LogInformation("Starting {Count} enabled source(s) (out of {Total})", enabled.Length, file.Sources.Count);

        var tasks = enabled.Select(s => RunSourceLoop(s, file.DefaultIntervalMinutes, stoppingToken)).ToArray();
        await Task.WhenAll(tasks);
    }

    private async Task RunSourceLoop(SourceConfig source, int defaultIntervalMinutes, CancellationToken ct)
    {
        var interval = TimeSpan.FromMinutes(source.IntervalMinutes ?? defaultIntervalMinutes);
        _log.LogInformation("Source '{Name}' [{Type}] every {Interval}", source.Name, source.Type, interval);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_monitors.TryGetValue(source.Type, out var monitor))
                {
                    _log.LogError("No monitor registered for type {Type}", source.Type);
                    return;
                }

                var rawItems = await monitor.FetchAsync(source, ct);
                var items = ApplyFilter(source, rawItems);
                var changes = await _detector.DetectAsync(source, items, ct);

                foreach (var c in changes)
                    await _notifier.NotifyAsync(c, ct);

                _log.LogInformation(
                    "Source '{Name}': fetched {Raw} item(s), {Kept} after filter, {Changes} change(s) notified",
                    source.Name, rawItems.Count, items.Count, changes.Count);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Source '{Name}' poll failed", source.Name);
            }

            try { await Task.Delay(interval, ct); }
            catch (OperationCanceledException) { return; }
        }
    }

    private IReadOnlyList<MonitorItem> ApplyFilter(SourceConfig source, IReadOnlyList<MonitorItem> items)
    {
        var pattern = source.Filter?.TitleMatches;
        if (string.IsNullOrWhiteSpace(pattern)) return items;

        var kept = new List<MonitorItem>(items.Count);
        foreach (var it in items)
        {
            var title = it.Title ?? "";
            // Regex.IsMatch uses an internal LRU cache keyed by (pattern, options),
            // so we don't need to compile per-source ourselves.
            if (Regex.IsMatch(title, pattern))
                kept.Add(it);
        }

        if (kept.Count != items.Count)
            _log.LogDebug("Source '{Source}': filter kept {Kept}/{Total} items", source.Name, kept.Count, items.Count);

        return kept;
    }
}
