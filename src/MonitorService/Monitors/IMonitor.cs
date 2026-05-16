using MonitorService.Configuration;

namespace MonitorService.Monitors;

public interface IMonitor
{
    SourceType Handles { get; }
    Task<IReadOnlyList<MonitorItem>> FetchAsync(SourceConfig source, CancellationToken ct);
}
