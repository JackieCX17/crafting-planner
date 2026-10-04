using System.Text.Json.Serialization;

namespace CraftingPlanner.Api.Contracts;

/// <summary>The main facts about an item. Used in lists.</summary>
public class ItemSummary
{
    /// <summary>Number that identifies the item.</summary>
    /// <example>11</example>
    public required int Id { get; init; }

    /// <summary>Display name.</summary>
    /// <example>Sword</example>
    public required string Name { get; init; }

    /// <summary>What the item is, when a description was given.</summary>
    /// <example>An iron blade on a wooden grip.</example>
    public string? Description { get; init; }

    /// <summary>Group the item belongs to, when one was given.</summary>
    /// <example>Weapon</example>
    public string? Category { get; init; }

    /// <summary>Name of the item's picture, when one was given. The file is at <c>/icons/{icon}.svg</c>.</summary>
    /// <example>broadsword</example>
    public string? Icon { get; init; }

    /// <summary>Raw when the item has no recipe, crafted when it has one.</summary>
    public required ItemKind Kind { get; init; }
}

/// <summary>
/// Everything about one item: its own facts, the recipe that makes it,
/// and the other items it is used to make.
/// </summary>
public class ItemDetail : ItemSummary
{
    // JsonPropertyOrder places these after the fields inherited from ItemSummary,
    // so a response starts with the id and name.

    /// <summary>The recipe that makes this item. Null when the item is raw.</summary>
    [JsonPropertyOrder(1)]
    public RecipeBrief? Recipe { get; init; }

    /// <summary>The items whose recipes use this item as an ingredient.</summary>
    [JsonPropertyOrder(2)]
    public required IReadOnlyList<ItemUse> UsedIn { get; init; }

    /// <summary>The shopping lists this item is on. Deleting the item removes it from them.</summary>
    [JsonPropertyOrder(3)]
    public required IReadOnlyList<ListUse> OnLists { get; init; }
}

/// <summary>A recipe as shown inside the item it makes.</summary>
public class RecipeBrief
{
    /// <summary>Number that identifies the recipe.</summary>
    /// <example>4</example>
    public required int Id { get; init; }

    /// <summary>How many units one craft makes.</summary>
    /// <example>1</example>
    public required int OutputQuantity { get; init; }

    /// <summary>How long one craft takes, in seconds.</summary>
    /// <example>8</example>
    public required int CraftSeconds { get; init; }

    /// <summary>The items one craft uses up.</summary>
    public required IReadOnlyList<IngredientLine> Ingredients { get; init; }
}

/// <summary>One ingredient of a recipe, with the quantity one craft uses.</summary>
public class IngredientLine
{
    /// <summary>Id of the ingredient item.</summary>
    /// <example>10</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the ingredient item.</summary>
    /// <example>Iron Ingot</example>
    public required string ItemName { get; init; }

    /// <summary>Units one craft uses up.</summary>
    /// <example>2</example>
    public required int Quantity { get; init; }
}

/// <summary>One place an item is used: "this many go into each craft of that item".</summary>
public class ItemUse
{
    /// <summary>Id of the recipe that uses the item.</summary>
    /// <example>4</example>
    public required int RecipeId { get; init; }

    /// <summary>Id of the item that recipe makes.</summary>
    /// <example>11</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the item that recipe makes.</summary>
    /// <example>Sword</example>
    public required string ItemName { get; init; }

    /// <summary>Units one craft of that item uses up.</summary>
    /// <example>2</example>
    public required int Quantity { get; init; }
}
