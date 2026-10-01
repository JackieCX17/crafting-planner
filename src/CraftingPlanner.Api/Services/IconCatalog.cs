using System.Text.Json;
using CraftingPlanner.Api.Contracts;
using Microsoft.AspNetCore.Hosting;

namespace CraftingPlanner.Api.Services;

/// <summary>
/// The pictures an item can be given: the SVG files in the website's <c>icons</c> folder,
/// with their authors from <c>icons.json</c>. Read once when the app starts.
/// </summary>
public sealed class IconCatalog
{
    /// <summary>The icons, in name order.</summary>
    public IReadOnlyList<IconInfo> Icons { get; }

    /// <summary>The icon names, for quick checks.</summary>
    private readonly HashSet<string> names;

    /// <summary>Reads the icons folder.</summary>
    /// <param name="environment">Where the website's files are. Supplied automatically.</param>
    public IconCatalog(IWebHostEnvironment environment)
    {
        var folder = Path.Combine(environment.WebRootPath ?? "wwwroot", "icons");
        var authors = ReadAuthors(Path.Combine(folder, "icons.json"));

        Icons = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.svg")
                .Select(file => Path.GetFileNameWithoutExtension(file))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => new IconInfo
                {
                    Name = name,
                    Author = authors.GetValueOrDefault(name, "Unknown"),
                    Url = $"/icons/{name}.svg",
                })
                .ToList()
            : [];

        names = Icons.Select(icon => icon.Name).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Checks whether an icon with this name is shipped with the website.</summary>
    /// <param name="name">The icon name.</param>
    /// <returns>True when the file exists.</returns>
    public bool Contains(string name) => names.Contains(name);

    /// <summary>Reads the icon authors from <c>icons.json</c>.</summary>
    /// <param name="file">Path of the file.</param>
    /// <returns>Icon name to author. Empty when the file is missing.</returns>
    private static Dictionary<string, string> ReadAuthors(string file)
    {
        if (!File.Exists(file))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(file));

        return document.RootElement
            .EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("name").GetString()!,
                entry => entry.GetProperty("author").GetString() ?? "Unknown");
    }
}
