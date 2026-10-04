namespace CraftingPlanner.Api.Contracts;

/// <summary>One of the pictures an item can be given.</summary>
public class IconInfo
{
    /// <summary>The name to put in an item's <c>icon</c> field.</summary>
    /// <example>broadsword</example>
    public required string Name { get; init; }

    /// <summary>Who drew it. The icons are used under the Creative Commons Attribution 3.0 license.</summary>
    /// <example>Lorc</example>
    public required string Author { get; init; }

    /// <summary>Where the picture file is served from.</summary>
    /// <example>/icons/broadsword.svg</example>
    public required string Url { get; init; }
}
