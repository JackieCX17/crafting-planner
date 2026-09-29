namespace CraftingPlanner.Api.Models;

/// <summary>
/// The instructions for crafting one item: what goes in, and how many come out.
/// One row in the Recipes table.
/// </summary>
/// <remarks>
/// An item has at most one recipe. The database enforces this by allowing each
/// <see cref="OutputItemId"/> to appear only once.
/// </remarks>
public class Recipe
{
    /// <summary>Number that identifies the recipe. Set by the database.</summary>
    public int Id { get; set; }

    /// <summary>Id of the item this recipe makes.</summary>
    public int OutputItemId { get; set; }

    /// <summary>The item this recipe makes.</summary>
    public Item OutputItem { get; set; } = null!;

    /// <summary>How many units one craft makes. 1 to 1,000.</summary>
    public int OutputQuantity { get; set; } = 1;

    /// <summary>How long one craft takes, in seconds. 0 or more.</summary>
    public int CraftSeconds { get; set; }

    /// <summary>The items one craft uses up, with the quantity of each.</summary>
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}
