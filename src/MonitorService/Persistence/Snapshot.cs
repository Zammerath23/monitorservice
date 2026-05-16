namespace MonitorService.Persistence;

public sealed record Snapshot(
    string SourceName,
    string ItemKey,
    string FieldsJson,
    string Hash,
    DateTimeOffset CapturedAt,
    string? Url,
    string? Title
);
