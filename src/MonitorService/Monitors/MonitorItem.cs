namespace MonitorService.Monitors;

public sealed record MonitorItem(
    string SourceName,
    string Key,
    IReadOnlyDictionary<string, string?> Fields,
    string? Url = null,
    string? Title = null
);
