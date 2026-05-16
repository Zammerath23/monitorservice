using Microsoft.Extensions.Logging;
using MonitorService.Detection;

namespace MonitorService.Notifications;

public sealed class ConsoleNotifier : INotifier
{
    private readonly ILogger<ConsoleNotifier> _log;

    public ConsoleNotifier(ILogger<ConsoleNotifier> log) => _log = log;

    public Task NotifyAsync(DetectedChange change, CancellationToken ct)
    {
        var kind = change.Kind == ChangeKind.NewItem ? "NEW" : "CHANGED";
        var title = change.Title ?? change.ItemKey;
        _log.LogInformation("[{Kind}] {Source} - {Title}", kind, change.SourceName, title);
        foreach (var d in change.Deltas)
            _log.LogInformation("  · {Field}: {Prev} -> {Curr}", d.Field, d.Previous ?? "(none)", d.Current ?? "(none)");
        if (!string.IsNullOrEmpty(change.Url))
            _log.LogInformation("  · {Url}", change.Url);
        return Task.CompletedTask;
    }
}
