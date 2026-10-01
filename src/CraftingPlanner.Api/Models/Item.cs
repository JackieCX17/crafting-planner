namespace CraftingPlanner.Api.Models;

/// <summary>
/// Something that can be crafted or used as an ingredient, such as "Iron Ingot" or "Stick".
/// One row in the Items table.
/// </summary>
/// <remarks>
/// An item is raw when it has no <see cref="Recipe"/>. That is worked out on request
/// and never stored, so the label cannot disagree with the data.
/// </remarks>
public class Item
{
    /// <summary>Number that identifies the item. Set by the database.</summary>
    public int Id { get; set; }

    /// <summary>Display name. Required, unique, 1 to 80 characters.</summary>
    public required string Name { get; set; }

    /// <summary>Optional explanation of what the item is. Up to 500 characters.</summary>
    public string? Description { get; set; }

    /// <summary>Optional group the item belongs to, such as "Tool". Up to 40 characters.</summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional name of the picture shown for the item, such as "broadsword". It names one of
    /// the icon files the website ships with. Up to 40 characters.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>The recipe that makes this item, or null when the item is raw.</summary>
    public Recipe? Recipe { get; set; }

    /// <summary>Every recipe line that uses this item as an ingredient.</summary>
    public List<RecipeIngredient> UsedIn { get; set; } = [];

    /// <summary>Every shopping list line that names this item.</summary>
    public List<ShoppingListEntry> OnLists { get; set; } = [];
}
