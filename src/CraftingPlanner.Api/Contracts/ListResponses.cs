namespace CraftingPlanner.Api.Contracts;

/// <summary>The main facts about a shopping list. Used in lists of lists.</summary>
public class ListSummary
{
    /// <summary>Number that identifies the list.</summary>
    /// <example>1</example>
    public required int Id { get; init; }

    /// <summary>Display name.</summary>
    /// <example>Starter gear</example>
    public required string Name { get; init; }

    /// <summary>What the list is for, when a note was given.</summary>
    /// <example>What a new player needs on day one.</example>
    public string? Description { get; init; }

    /// <summary>How many different items are on the list.</summary>
    /// <example>3</example>
    public required int EntryCount { get; init; }
}

/// <summary>A shopping list with everything on it.</summary>
public class ListDetail : ListSummary
{
    /// <summary>The items on the list, in name order.</summary>
    public required IReadOnlyList<ListEntryLine> Entries { get; init; }
}

/// <summary>One item on a shopping list, with how many of it to make.</summary>
public class ListEntryLine
{
    /// <summary>Id of the item.</summary>
    /// <example>11</example>
    public required int ItemId { get; init; }

    /// <summary>Name of the item.</summary>
    /// <example>Sword</example>
    public required string ItemName { get; init; }

    /// <summary>Group the item belongs to, when one was given.</summary>
    /// <example>Weapon</example>
    public string? Category { get; init; }

    /// <summary>Name of the item's picture, when one was given.</summary>
    /// <example>broadsword</example>
    public string? Icon { get; init; }

    /// <summary>Raw when the item has no recipe, crafted when it has one.</summary>
    public required ItemKind Kind { get; init; }

    /// <summary>How many units to make.</summary>
    /// <example>2</example>
    public required long Quantity { get; init; }
}

/// <summary>One place an item appears on a shopping list.</summary>
public class ListUse
{
    /// <summary>Id of the list.</summary>
    /// <example>1</example>
    public required int ListId { get; init; }

    /// <summary>Name of the list.</summary>
    /// <example>Starter gear</example>
    public required string ListName { get; init; }

    /// <summary>How many units of the item the list asks for.</summary>
    /// <example>2</example>
    public required long Quantity { get; init; }
}

/// <summary>The answer to "what do I need to make everything on this list?".</summary>
public class ListPlanResponse
{
    /// <summary>Id of the list.</summary>
    /// <example>1</example>
    public required int ListId { get; init; }

    /// <summary>Name of the list.</summary>
    /// <example>Starter gear</example>
    public required string ListName { get; init; }

    /// <summary>What the plan makes: the list's entries, in name order.</summary>
    public required IReadOnlyList<ListEntryLine> Targets { get; init; }

    /// <summary>The raw items to gather, with their totals, in name order.</summary>
    public required IReadOnlyList<RawMaterialLine> RawMaterials { get; init; }

    /// <summary>
    /// The crafted items in the order to craft them. An item comes after every crafted
    /// ingredient it needs. A target that another target needs, such as a Sword on a list with
    /// a Tool Kit, appears once with its own quantity and the kit's share added together.
    /// </summary>
    public required IReadOnlyList<PlanStepLine> Steps { get; init; }

    /// <summary>The time for every craft in every step, in seconds.</summary>
    /// <example>98</example>
    public required long TotalSeconds { get; init; }
}
