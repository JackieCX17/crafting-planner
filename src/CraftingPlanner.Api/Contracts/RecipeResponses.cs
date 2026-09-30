namespace CraftingPlanner.Api.Contracts;

/// <summary>A recipe: the item it makes, how many, how long it takes, and what it uses.</summary>
public class RecipeDetail
{
    /// <summary>Number that identifies the recipe.</summary>
    /// <example>4</example>
    public required int Id { get; init; }

    /// <summary>Id of the item the recipe makes.</summary>
    /// <example>11</example>
    public required int OutputItemId { get; init; }

    /// <summary>Name of the item the recipe makes.</summary>
    /// <example>Sword</example>
    public required string OutputItemName { get; init; }

    /// <summary>How many units one craft makes.</summary>
    /// <example>1</example>
    public required int OutputQuantity { get; init; }

    /// <summary>How long one craft takes, in seconds.</summary>
    /// <example>8</example>
    public required int CraftSeconds { get; init; }

    /// <summary>The items one craft uses up, in name order.</summary>
    public required IReadOnlyList<IngredientLine> Ingredients { get; init; }
}
