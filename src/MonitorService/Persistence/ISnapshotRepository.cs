namespace MonitorService.Persistence;

public interface ISnapshotRepository
{
    Task InitializeAsync(CancellationToken ct);
    Task<bool> HasAnyAsync(string sourceName, CancellationToken ct);
    Task<Snapshot?> GetLatestAsync(string sourceName, string itemKey, CancellationToken ct);
    Task SaveAsync(Snapshot snapshot, CancellationToken ct);
}
