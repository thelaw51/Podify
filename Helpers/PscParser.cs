using System.Globalization;
using System.Xml.Linq;
using Podify.Models;

namespace Podify.Helpers;

public static class PscParser
{
    public static readonly XNamespace Ns = "http://podlove.org/simple-chapters";

    public static List<Chapter> Parse(XElement item)
    {
        var chapters = new List<Chapter>();
        var container = item.Element(Ns + "chapters");
        if (container is null) return chapters;

        foreach (var node in container.Elements(Ns + "chapter"))
        {
            if (!TryParseTime(node.Attribute("start")?.Value, out var start)) continue;
            var title = node.Attribute("title")?.Value?.Trim() ?? string.Empty;
            chapters.Add(new Chapter
            {
                Start = start,
                Title = title,
                ImageUrl = node.Attribute("image")?.Value,
                Url = node.Attribute("href")?.Value
            });
        }

        chapters.Sort((a, b) => a.Start.CompareTo(b.Start));
        return chapters;
    }

    private static bool TryParseTime(string? value, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Trim().Split(':');
        try
        {
            switch (parts.Length)
            {
                case 1:
                    if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                    {
                        result = TimeSpan.FromSeconds(seconds);
                        return true;
                    }
                    return false;
                case 2:
                    result = new TimeSpan(0, int.Parse(parts[0], CultureInfo.InvariantCulture), 0) +
                             TimeSpan.FromSeconds(double.Parse(parts[1], CultureInfo.InvariantCulture));
                    return true;
                case 3:
                    result = new TimeSpan(int.Parse(parts[0], CultureInfo.InvariantCulture),
                                          int.Parse(parts[1], CultureInfo.InvariantCulture), 0) +
                             TimeSpan.FromSeconds(double.Parse(parts[2], CultureInfo.InvariantCulture));
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }
}
