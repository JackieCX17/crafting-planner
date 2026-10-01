using System.Text.RegularExpressions;

namespace CraftingPlanner.Api.Services;

/// <summary>Turns display names into pieces of file names.</summary>
public static partial class FileNames
{
    /// <summary>
    /// Makes a name safe for a file name: lower case, with every run of characters other
    /// than letters and digits replaced by one hyphen. "Tool Kit" becomes "tool-kit".
    /// </summary>
    /// <param name="name">The display name.</param>
    /// <returns>The slug, or "item" when nothing usable is left.</returns>
    public static string Slug(string name)
    {
        var slug = NotLetterOrDigit().Replace(name.ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "item" : slug;
    }

    /// <summary>Matches every run of characters that cannot go in a file name.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotLetterOrDigit();
}
