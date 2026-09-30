using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CraftingPlanner.Api.Contracts;

/// <summary>
/// What a caller sends to replace a recipe (PUT): how many it makes, how long it takes,
/// and the full list of ingredients.
/// </summary>
/// <remarks>
/// The item a recipe makes cannot be changed. To make a different item, create a new recipe.
/// </remarks>
public class RecipeReplaceRequest
{
    /// <summary>How many units one craft makes. 1 to 1,000. Defaults to 1.</summary>
    /// <example>4</example>
    [Range(1, 1000, ErrorMessage = RecipeRules.OutputQuantityRange)]
    public int OutputQuantity { get; set; } = 1;

    /// <summary>How long one craft takes, in seconds. 0 to 86,400, which is one day. Defaults to 0.</summary>
    /// <example>5</example>
    [Range(0, 86_400, ErrorMessage = RecipeRules.CraftSecondsRange)]
    public int CraftSeconds { get; set; }

    /// <summary>The items one craft uses up. 1 to 20 ingredients, each item listed once.</summary>
    [Required(ErrorMessage = RecipeRules.IngredientCount)]
    [MinLength(1, ErrorMessage = RecipeRules.IngredientCount)]
    [MaxLength(RecipeRules.MostIngredients, ErrorMessage = RecipeRules.IngredientCount)]
    public List<IngredientRequest> Ingredients { get; set; } = [];
}

/// <summary>
/// What a caller sends to create a recipe (POST): the item it makes, plus everything
/// in <see cref="RecipeReplaceRequest"/>.
/// </summary>
public class RecipeCreateRequest : RecipeReplaceRequest
{
    /// <summary>Id of the item the recipe makes. The item must exist and must not have a recipe yet.</summary>
    /// <example>20</example>
    [JsonPropertyOrder(-1)]
    [Range(1, int.MaxValue, ErrorMessage = RecipeRules.OutputItemRequired)]
    public int OutputItemId { get; set; }
}

/// <summary>One ingredient in a recipe that is being created or replaced.</summary>
public class IngredientRequest
{
    /// <summary>Id of the item to use. The item must exist.</summary>
    /// <example>8</example>
    [Range(1, int.MaxValue, ErrorMessage = RecipeRules.IngredientItemRequired)]
    public int ItemId { get; set; }

    /// <summary>Units one craft uses up. 1 to 1,000.</summary>
    /// <example>2</example>
    [Range(1, 1000, ErrorMessage = RecipeRules.IngredientQuantityRange)]
    public int Quantity { get; set; }
}

/// <summary>
/// What a caller sends to add one ingredient to a recipe, or to change its quantity.
/// The recipe and the item are named in the address.
/// </summary>
public class IngredientQuantityRequest
{
    /// <summary>Units one craft uses up. 1 to 1,000.</summary>
    /// <example>3</example>
    [Range(1, 1000, ErrorMessage = RecipeRules.IngredientQuantityRange)]
    public int Quantity { get; set; }
}

/// <summary>
/// The limits on a recipe and the messages sent back when one is broken. Kept in one
/// place so that every endpoint words the same problem the same way.
/// </summary>
public static class RecipeRules
{
    /// <summary>The largest number of ingredients one recipe can have.</summary>
    public const int MostIngredients = 20;

    /// <summary>Sent when the item the recipe makes is missing.</summary>
    public const string OutputItemRequired = "The output item id is required.";

    /// <summary>Sent when the number made per craft is out of range.</summary>
    public const string OutputQuantityRange = "The output quantity must be 1 to 1,000.";

    /// <summary>Sent when the craft time is out of range.</summary>
    public const string CraftSecondsRange = "The craft time must be 0 to 86,400 seconds.";

    /// <summary>Sent when a recipe has no ingredients or too many.</summary>
    public const string IngredientCount = "A recipe needs 1 to 20 ingredients.";

    /// <summary>Sent when an ingredient does not say which item it is.</summary>
    public const string IngredientItemRequired = "The ingredient's item id is required.";

    /// <summary>Sent when an ingredient's quantity is out of range.</summary>
    public const string IngredientQuantityRange = "The quantity must be 1 to 1,000.";
}
