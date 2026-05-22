namespace MonitorService.Detection;

public enum ChangeKind
{
    NewItem,
    FieldChanged
}

public sealed record FieldDelta(string Field, string? Previous, string? Current);

public sealed record DetectedChange(
    string SourceName,
    string ItemKey,
    ChangeKind Kind,
    IReadOnlyList<FieldDelta> Deltas,
    string? Url,
    string? Title,
    DateTimeOffset DetectedAt,
    string? DiscordMention = null
);
