namespace CraftingPlanner.Api.Services;

// The plain data the planning calculation works on. None of it knows about the
// database or the web, so the calculation can be tested with hand-written values.
//
// These are records: classes that only hold data, compare by value, and cannot be
// changed after they are created. The parameters in brackets become their fields.

/// <summary>How one item is made, as the calculation sees it.</summary>
/// <param name="OutputQuantity">How many units one craft produces.</param>
/// <param name="CraftSeconds">How long one craft takes.</param>
/// <param name="Ingredients">The items one craft uses up.</param>
public sealed record PlanRecipe(int OutputQuantity, int CraftSeconds, IReadOnlyList<PlanIngredient> Ingredients);

/// <summary>One ingredient of a recipe, as the calculation sees it.</summary>
/// <param name="ItemId">Id of the ingredient item.</param>
/// <param name="Quantity">Units one craft uses up.</param>
public sealed record PlanIngredient(int ItemId, int Quantity);

/// <summary>One crafted item in a plan.</summary>
/// <param name="ItemId">Id of the crafted item.</param>
/// <param name="Needed">Units required in total, by the target and by other steps.</param>
/// <param name="Crafts">How many times the recipe is run: Needed divided by the recipe's output, rounded up.</param>
/// <param name="Made">Units produced: Crafts times the recipe's output.</param>
/// <param name="Leftover">Units produced but not needed: Made minus Needed.</param>
/// <param name="Seconds">Time for all the crafts of this step.</param>
public sealed record PlanStep(int ItemId, long Needed, long Crafts, long Made, long Leftover, long Seconds);

/// <summary>One raw material in a plan.</summary>
/// <param name="ItemId">Id of the raw item.</param>
/// <param name="Quantity">Units to gather.</param>
public sealed record RawMaterial(int ItemId, long Quantity);

/// <summary>The answer to "what do I need to make this?".</summary>
/// <param name="RawMaterials">The raw items to gather, with their totals.</param>
/// <param name="Steps">
/// The crafted items in the order to craft them: an item comes after every crafted
/// ingredient it needs, so its ingredients are ready when its turn comes.
/// </param>
/// <param name="TotalSeconds">The time for every craft in every step.</param>
public sealed record PlanResult(IReadOnlyList<RawMaterial> RawMaterials, IReadOnlyList<PlanStep> Steps, long TotalSeconds);

/// <summary>
/// Thrown when a total in the plan would not fit in a 64-bit whole number. This takes a
/// long chain of recipes with large quantities at every level, and a caller who asks for
/// it should be told to try a smaller quantity.
/// </summary>
/// <param name="itemId">Id of the item whose total was too large.</param>
public sealed class PlanTooLargeException(int itemId)
    : Exception($"The total for item {itemId} does not fit in a 64-bit whole number.")
{
    /// <summary>Id of the item whose total was too large.</summary>
    public int ItemId { get; } = itemId;
}
