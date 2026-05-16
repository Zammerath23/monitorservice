using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MonitorService.Configuration;

public sealed class SourcesLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public SourcesFile Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"sources file not found: {Path.GetFullPath(path)}");

        var json = File.ReadAllText(path);
        var file = JsonSerializer.Deserialize<SourcesFile>(json, JsonOpts)
                   ?? throw new InvalidOperationException("sources file is empty or invalid");

        Validate(file);
        return file;
    }

    private static void Validate(SourcesFile file)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < file.Sources.Count; i++)
        {
            var s = file.Sources[i];
            var label = string.IsNullOrWhiteSpace(s.Name) ? $"#{i}" : s.Name;

            if (string.IsNullOrWhiteSpace(s.Name))
                errors.Add($"source {label}: 'name' is required");
            else if (!seen.Add(s.Name))
                errors.Add($"source {label}: duplicate name");

            if (string.IsNullOrWhiteSpace(s.Url))
                errors.Add($"source {label}: 'url' is required");

            if (s.Type != SourceType.Rss && (s.Watch is null || s.Watch.Count == 0))
                errors.Add($"source {label}: 'watch' must contain at least one field for type '{s.Type}'");

            if (!string.IsNullOrWhiteSpace(s.Filter?.TitleMatches))
            {
                try { _ = new Regex(s.Filter!.TitleMatches!); }
                catch (ArgumentException ex)
                {
                    errors.Add($"source {label}: filter.titleMatches is not a valid regex ({ex.Message})");
                }
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid sources.json:\n  - " + string.Join("\n  - ", errors));
    }
}
