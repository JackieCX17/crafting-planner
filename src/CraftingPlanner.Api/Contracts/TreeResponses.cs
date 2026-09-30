namespace CraftingPlanner.Api.Contracts;

/// <summary>
/// One item in a recipe tree, with the ingredients of its recipe nested inside it.
/// The tree shows structure, not totals: an item that two recipes use appears under each.
/// </summary>
public class TreeNode
{
    /// <summary>Id of the item.</summary>
    /// <example>9</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the item.</summary>
    /// <example>Stick</example>
    public required string ItemName { get; init; }

    /// <summary>Raw when the item has no recipe, crafted when it has one.</summary>
    public required ItemKind Kind { get; init; }

    /// <summary>Units of this item that one craft of the item above it uses. 1 for the item at the top.</summary>
    /// <example>2</example>
    public required int Quantity { get; init; }

    /// <summary>How many units one craft makes. Null for a raw item.</summary>
    /// <example>4</example>
    public int? OutputQuantity { get; init; }

    /// <summary>How long one craft takes, in seconds. Null for a raw item.</summary>
    /// <example>2</example>
    public int? CraftSeconds { get; init; }

    /// <summary>The items one craft uses up, each with its own ingredients inside. Empty for a raw item.</summary>
    public List<TreeNode> Ingredients { get; init; } = [];

    /// <summary>
    /// True when this item has a recipe but its ingredients were left out because the tree
    /// reached its size limit. Ask for the item's own tree to see them.
    /// </summary>
    public bool Truncated { get; set; }
}
