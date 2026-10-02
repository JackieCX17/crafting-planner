namespace CraftingPlanner.Api.Models;

/// <summary>
/// One line of a shopping list: "this many of that item".
/// One row in the ShoppingListEntries table.
/// </summary>
/// <remarks>
/// <see cref="ListId"/> and <see cref="ItemId"/> together identify the row, so an item
/// appears only once on a list.
/// </remarks>
public class ShoppingListEntry
{
    /// <summary>Id of the list this line belongs to.</summary>
    public int ListId { get; set; }

    /// <summary>The list this line belongs to.</summary>
    public ShoppingList List { get; set; } = null!;

    /// <summary>Id of the item to make.</summary>
    public int ItemId { get; set; }

    /// <summary>The item to make.</summary>
    public Item Item { get; set; } = null!;

    /// <summary>How many units to make. 1 to 1,000,000.</summary>
    public long Quantity { get; set; }
}
