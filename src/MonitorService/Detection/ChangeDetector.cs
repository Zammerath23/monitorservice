using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MonitorService.Configuration;
using MonitorService.Monitors;
using MonitorService.Persistence;

namespace MonitorService.Detection;

public sealed class ChangeDetector
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly ISnapshotRepository _repo;

    public ChangeDetector(ISnapshotRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<DetectedChange>> DetectAsync(
        SourceConfig source,
        IReadOnlyList<MonitorItem> items,
        CancellationToken ct)
    {
        var alertOn = source.AlertOn is { Count: > 0 }
            ? new HashSet<string>(source.AlertOn, StringComparer.OrdinalIgnoreCase)
            : null;

        // For RSS sources without an explicit alertOn, only NewItem is interesting:
        // sites often re-touch pubDate / summary of existing items, which would
        // otherwise generate notification storms (and burn through Discord rate
        // limits). The snapshot is still updated below so we don't keep flagging
        // the same change.
        var suppressFieldChanges = source.Type == SourceType.Rss && alertOn is null;

        // "Seeding" = this source has no snapshots yet. Computed once per batch
        // so the loop below treats every item in this fetch as seed if applicable.
        var seeding = source.SeedSilently && !await _repo.HasAnyAsync(source.Name, ct);

        var changes = new List<DetectedChange>();

        foreach (var item in items)
        {
            var previous = await _repo.GetLatestAsync(item.SourceName, item.Key, ct);
            var fieldsJson = JsonSerializer.Serialize(item.Fields, JsonOpts);
            var hash = Hash(fieldsJson);

            if (previous is null)
            {
                if (!seeding)
                {
                    changes.Add(new DetectedChange(
                        item.SourceName, item.Key, ChangeKind.NewItem,
                        Deltas: item.Fields.Select(kv => new FieldDelta(kv.Key, null, kv.Value)).ToArray(),
                        Url: item.Url, Title: item.Title, DetectedAt: DateTimeOffset.UtcNow));
                }
            }
            else if (!string.Equals(previous.Hash, hash, StringComparison.Ordinal))
            {
                if (!suppressFieldChanges)
                {
                    var prevFields = JsonSerializer.Deserialize<Dictionary<string, string?>>(previous.FieldsJson, JsonOpts)
                                     ?? new Dictionary<string, string?>();

                    var deltas = item.Fields
                        .Where(kv => !string.Equals(prevFields.GetValueOrDefault(kv.Key), kv.Value, StringComparison.Ordinal))
                        .Where(kv => alertOn is null || alertOn.Contains(kv.Key))
                        .Select(kv => new FieldDelta(kv.Key, prevFields.GetValueOrDefault(kv.Key), kv.Value))
                        .ToArray();

                    if (deltas.Length > 0)
                    {
                        changes.Add(new DetectedChange(
                            item.SourceName, item.Key, ChangeKind.FieldChanged,
                            Deltas: deltas, Url: item.Url, Title: item.Title,
                            DetectedAt: DateTimeOffset.UtcNow));
                    }
                }
            }
            else
            {
                continue;
            }

            await _repo.SaveAsync(new Snapshot(
                item.SourceName, item.Key, fieldsJson, hash,
                DateTimeOffset.UtcNow, item.Url, item.Title), ct);
        }

        return changes;
    }

    private static string Hash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}
