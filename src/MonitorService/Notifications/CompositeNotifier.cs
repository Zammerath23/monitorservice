using MonitorService.Detection;

namespace MonitorService.Notifications;

public sealed class CompositeNotifier : INotifier
{
    private readonly IEnumerable<INotifier> _notifiers;
    public CompositeNotifier(IEnumerable<INotifier> notifiers) => _notifiers = notifiers;

    public async Task NotifyAsync(DetectedChange change, CancellationToken ct)
    {
        foreach (var n in _notifiers)
            await n.NotifyAsync(change, ct);
    }
}
