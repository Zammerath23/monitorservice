using System.Text.Json.Serialization;

namespace MonitorService.Configuration;

public enum SourceType
{
    Rss,
    Html,
    Playwright
}

public sealed class SourceConfig
{
    public string Name { get; set; } = "";
    public SourceType Type { get; set; }
    public string Url { get; set; } = "";
    public int? IntervalMinutes { get; set; }
    public bool Enabled { get; set; } = true;

    public Dictionary<string, FieldSelector>? Watch { get; set; }
    public List<string>? AlertOn { get; set; }
    public string? WaitFor { get; set; }
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// First time we fetch this source (no snapshots in DB yet), save snapshots but
    /// do not emit "new item" notifications. Useful for RSS feeds whose backlog
    /// would otherwise spam the notifier on first start.
    /// </summary>
    public bool SeedSilently { get; set; }

    /// <summary>
    /// Optional content filter applied after fetching. Items that don't match are
    /// dropped before change detection — they are NOT saved to the DB.
    /// </summary>
    public SourceFilter? Filter { get; set; }
}

public sealed class SourceFilter
{
    /// <summary>
    /// .NET regex applied to the item title. If set, only items whose title matches
    /// are kept. Use the inline option <c>(?i)</c> for case-insensitive matches.
    /// Example: <c>"(?i)\\bmtg\\b|magic.*gathering"</c>.
    /// </summary>
    public string? TitleMatches { get; set; }
}

public sealed class FieldSelector
{
    public string Selector { get; set; } = "";

    [JsonPropertyName("attribute")]
    public string Attribute { get; set; } = "text";
}

public sealed class SourcesFile
{
    public int DefaultIntervalMinutes { get; set; } = 15;
    public List<SourceConfig> Sources { get; set; } = new();
}
