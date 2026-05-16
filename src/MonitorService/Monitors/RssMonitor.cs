using CodeHollow.FeedReader;
using MonitorService.Configuration;

namespace MonitorService.Monitors;

public sealed class RssMonitor : IMonitor
{
    public SourceType Handles => SourceType.Rss;

    public async Task<IReadOnlyList<MonitorItem>> FetchAsync(SourceConfig source, CancellationToken ct)
    {
        var feed = await FeedReader.ReadAsync(source.Url, ct);
        var items = new List<MonitorItem>(feed.Items.Count);

        foreach (var entry in feed.Items)
        {
            var key = entry.Id ?? entry.Link ?? entry.Title;
            if (string.IsNullOrWhiteSpace(key)) continue;

            var fields = new Dictionary<string, string?>
            {
                ["title"]     = entry.Title,
                ["link"]      = entry.Link,
                ["published"] = entry.PublishingDate?.ToString("O"),
                ["summary"]   = Truncate(entry.Description, 500)
            };

            items.Add(new MonitorItem(
                SourceName: source.Name,
                Key: key,
                Fields: fields,
                Url: entry.Link,
                Title: entry.Title
            ));
        }

        return items;
    }

    private static string? Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= max ? text : text[..max];
    }
}
