namespace CraftingPlanner.Api.Contracts;

/// <summary>Whether an item has to be gathered or can be crafted.</summary>
public enum ItemKind
{
    /// <summary>The item has no recipe, so it has to be gathered.</summary>
    Raw,

    /// <summary>The item has a recipe, so it can be crafted from other items.</summary>
    Crafted,
}

/// <summary>The field a list of items is sorted by.</summary>
public enum ItemSort
{
    /// <summary>Sort by name, ignoring upper and lower case.</summary>
    Name,

    /// <summary>Sort by category, then by name within each category.</summary>
    Category,

    /// <summary>Sort by id, which is the order the items were created in.</summary>
    Id,
}

/// <summary>The direction a list is sorted in.</summary>
public enum SortOrder
{
    /// <summary>Smallest first: A to Z, or 1 upwards.</summary>
    Asc,

    /// <summary>Largest first: Z to A, or highest number downwards.</summary>
    Desc,
}
