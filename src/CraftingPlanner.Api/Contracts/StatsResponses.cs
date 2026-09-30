namespace CraftingPlanner.Api.Contracts;

/// <summary>Totals and highlights across all the data.</summary>
public class StatsResponse
{
    /// <summary>How many items there are.</summary>
    /// <example>19</example>
    public required int ItemCount { get; init; }

    /// <summary>How many items have no recipe.</summary>
    /// <example>7</example>
    public required int RawItemCount { get; init; }

    /// <summary>How many items have a recipe. The same as the number of recipes.</summary>
    /// <example>12</example>
    public required int CraftedItemCount { get; init; }

    /// <summary>How many different categories the items use.</summary>
    /// <example>6</example>
    public required int CategoryCount { get; init; }

    /// <summary>How many ingredient lines there are across every recipe.</summary>
    /// <example>22</example>
    public required int IngredientLineCount { get; init; }

    /// <summary>The item that the most recipes use as an ingredient. Null when there are no recipes.</summary>
    public required IngredientUse? MostUsedIngredient { get; init; }

    /// <summary>The crafted item with the longest chain of recipes beneath it. Null when there are no recipes.</summary>
    public required ChainLength? LongestChain { get; init; }
}

/// <summary>An item and how many recipes use it.</summary>
public class IngredientUse
{
    /// <summary>Id of the item.</summary>
    /// <example>9</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the item.</summary>
    /// <example>Stick</example>
    public required string ItemName { get; init; }

    /// <summary>How many recipes use it as an ingredient.</summary>
    /// <example>5</example>
    public required int RecipeCount { get; init; }
}

/// <summary>An item and the length of the recipe chain beneath it.</summary>
public class ChainLength
{
    /// <summary>Id of the item.</summary>
    /// <example>18</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the item.</summary>
    /// <example>Tool Kit</example>
    public required string ItemName { get; init; }

    /// <summary>
    /// How many recipes deep the chain goes. A raw item is 0. An item made only from raw
    /// items is 1. A Tool Kit made from a Sword made from a Stick made from a Plank made
    /// from a Log is 4.
    /// </summary>
    /// <example>4</example>
    public required int Depth { get; init; }
}
