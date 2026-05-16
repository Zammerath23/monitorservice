using MonitorService.Detection;

namespace MonitorService.Notifications;

public interface INotifier
{
    Task NotifyAsync(DetectedChange change, CancellationToken ct);
}
