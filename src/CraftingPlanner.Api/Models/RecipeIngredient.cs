namespace CraftingPlanner.Api.Models;

/// <summary>
/// One line of a recipe: "this recipe uses this many of that item".
/// One row in the RecipeIngredients table.
/// </summary>
/// <remarks>
/// <see cref="RecipeId"/> and <see cref="ItemId"/> together identify the row,
/// so an item can appear only once in a recipe.
/// </remarks>
public class RecipeIngredient
{
    /// <summary>Id of the recipe this line belongs to.</summary>
    public int RecipeId { get; set; }

    /// <summary>The recipe this line belongs to.</summary>
    public Recipe Recipe { get; set; } = null!;

    /// <summary>Id of the item being used as an ingredient.</summary>
    public int ItemId { get; set; }

    /// <summary>The item being used as an ingredient.</summary>
    public Item Item { get; set; } = null!;

    /// <summary>Units of the ingredient that one craft uses up. 1 to 1,000.</summary>
    public int Quantity { get; set; }
}
