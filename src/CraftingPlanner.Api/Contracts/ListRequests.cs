using System.ComponentModel.DataAnnotations;

namespace CraftingPlanner.Api.Contracts;

/// <summary>What a caller sends to create a shopping list (POST) or rename one (PUT).</summary>
public class ListRequest
{
    /// <summary>Display name. Required, unique, 1 to 80 characters.</summary>
    /// <example>Starter gear</example>
    [Required(ErrorMessage = ListRules.NameRequired)]
    [StringLength(80, MinimumLength = 1, ErrorMessage = ListRules.NameLength)]
    public string Name { get; set; } = "";

    /// <summary>Optional note about what the list is for. Up to 500 characters.</summary>
    /// <example>What a new player needs on day one.</example>
    [StringLength(500, ErrorMessage = ListRules.DescriptionLength)]
    public string? Description { get; set; }
}

/// <summary>
/// What a caller sends to put an item on a list, or to change how many of it the list asks for.
/// The list and the item are named in the address.
/// </summary>
public class ListEntryRequest
{
    /// <summary>How many units to make. 1 to 1,000,000.</summary>
    /// <example>2</example>
    [Range(1, 1_000_000, ErrorMessage = ListRules.QuantityRange)]
    public long Quantity { get; set; }
}

/// <summary>
/// The limits on a shopping list and the messages sent back when one is broken. Kept in one
/// place so that every endpoint words the same problem the same way.
/// </summary>
public static class ListRules
{
    /// <summary>The largest number of items one list can hold.</summary>
    public const int MostEntries = 50;

    /// <summary>Sent when the name is missing or blank.</summary>
    public const string NameRequired = "The name is required.";

    /// <summary>Sent when the name is too short or too long.</summary>
    public const string NameLength = "The name must be 1 to 80 characters long.";

    /// <summary>Sent when the description is too long.</summary>
    public const string DescriptionLength = "The description can be up to 500 characters long.";

    /// <summary>Sent when a quantity is out of range.</summary>
    public const string QuantityRange = "The quantity must be 1 to 1,000,000.";
}
