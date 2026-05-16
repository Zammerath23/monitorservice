using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using MonitorService.Configuration;

namespace MonitorService.Monitors;

internal static class FieldExtractor
{
    public static Dictionary<string, string?> ExtractFromHtml(
        HtmlDocument doc,
        IReadOnlyDictionary<string, FieldSelector> watch)
    {
        var result = new Dictionary<string, string?>(watch.Count);
        foreach (var (name, sel) in watch)
        {
            var node = doc.DocumentNode.QuerySelector(sel.Selector);
            result[name] = node is null ? null : ExtractValue(node, sel.Attribute);
        }
        return result;
    }

    private static string? ExtractValue(HtmlNode node, string attribute)
    {
        if (string.Equals(attribute, "text", StringComparison.OrdinalIgnoreCase))
            return Normalize(node.InnerText);
        if (string.Equals(attribute, "html", StringComparison.OrdinalIgnoreCase))
            return Normalize(node.InnerHtml);

        var value = node.GetAttributeValue(attribute, null!);
        return Normalize(value);
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var decoded = System.Net.WebUtility.HtmlDecode(value);
        return string.Join(' ', decoded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
