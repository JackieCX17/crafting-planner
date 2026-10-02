namespace CraftingPlanner.Api.Models;

/// <summary>
/// A named list of items to make, each with a quantity, such as "Starter gear: 2 Swords,
/// 1 Pickaxe, 16 Torches". Planning a list totals everything on it together.
/// One row in the ShoppingLists table.
/// </summary>
public class ShoppingList
{
    /// <summary>Number that identifies the list. Set by the database.</summary>
    public int Id { get; set; }

    /// <summary>Display name. Required, unique, 1 to 80 characters.</summary>
    public required string Name { get; set; }

    /// <summary>Optional note about what the list is for. Up to 500 characters.</summary>
    public string? Description { get; set; }

    /// <summary>The items on the list, with their quantities.</summary>
    public List<ShoppingListEntry> Entries { get; set; } = [];
}
