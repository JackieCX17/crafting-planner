namespace CraftingPlanner.Api.Contracts;

/// <summary>The answer to "what do I need to make this?".</summary>
public class PlanResponse
{
    /// <summary>Id of the item being made.</summary>
    /// <example>18</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the item being made.</summary>
    /// <example>Tool Kit</example>
    public required string ItemName { get; init; }

    /// <summary>How many units of it the plan makes.</summary>
    /// <example>3</example>
    public required long Quantity { get; init; }

    /// <summary>The raw items to gather, with their totals, in name order.</summary>
    public required IReadOnlyList<RawMaterialLine> RawMaterials { get; init; }

    /// <summary>
    /// The crafted items in the order to craft them. An item comes after every crafted
    /// ingredient it needs, so its ingredients are ready when its turn comes.
    /// </summary>
    public required IReadOnlyList<PlanStepLine> Steps { get; init; }

    /// <summary>The time for every craft in every step, in seconds.</summary>
    /// <example>222</example>
    public required long TotalSeconds { get; init; }
}

/// <summary>One raw material in a plan.</summary>
public class RawMaterialLine
{
    /// <summary>Id of the raw item.</summary>
    /// <example>2</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the raw item.</summary>
    /// <example>Iron Ore</example>
    public required string ItemName { get; init; }

    /// <summary>Units to gather.</summary>
    /// <example>30</example>
    public required long Quantity { get; init; }
}

/// <summary>One crafted item in a plan.</summary>
public class PlanStepLine
{
    /// <summary>Id of the crafted item.</summary>
    /// <example>9</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the crafted item.</summary>
    /// <example>Stick</example>
    public required string ItemName { get; init; }

    /// <summary>Units required in total, by the target and by other steps.</summary>
    /// <example>9</example>
    public required long Needed { get; init; }

    /// <summary>How many times to run the recipe: Needed divided by the recipe's output, rounded up.</summary>
    /// <example>3</example>
    public required long Crafts { get; init; }

    /// <summary>Units produced: Crafts times the recipe's output.</summary>
    /// <example>12</example>
    public required long Made { get; init; }

    /// <summary>Units produced but not needed: Made minus Needed.</summary>
    /// <example>3</example>
    public required long Leftover { get; init; }

    /// <summary>Time for all the crafts of this step, in seconds.</summary>
    /// <example>6</example>
    public required long Seconds { get; init; }
}
