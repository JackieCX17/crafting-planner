using System.ComponentModel.DataAnnotations;

namespace CraftingPlanner.Api.Contracts;

/// <summary>
/// What a caller sends to create an item (POST) or to replace every field of one (PUT).
/// </summary>
/// <remarks>
/// The labels in square brackets are the rules. They are checked automatically before
/// the endpoint's code runs, and they also appear on the interactive API page.
/// </remarks>
public class ItemRequest
{
    /// <summary>Display name. Required, unique, 1 to 80 characters.</summary>
    /// <example>Copper Ingot</example>
    [Required(ErrorMessage = ItemRules.NameRequired)]
    [StringLength(80, MinimumLength = 1, ErrorMessage = ItemRules.NameLength)]
    public string Name { get; set; } = "";

    /// <summary>Optional explanation of what the item is. Up to 500 characters.</summary>
    /// <example>A bar of smelted copper.</example>
    [StringLength(500, ErrorMessage = ItemRules.DescriptionLength)]
    public string? Description { get; set; }

    /// <summary>Optional group the item belongs to. Up to 40 characters.</summary>
    /// <example>Component</example>
    [StringLength(40, ErrorMessage = ItemRules.CategoryLength)]
    public string? Category { get; set; }
}

/// <summary>
/// What a caller sends to change part of an item (PATCH). Only the fields that are
/// sent are changed.
/// </summary>
/// <remarks>
/// Leave a field out to keep its current value. Send an empty string for
/// <see cref="Description"/> or <see cref="Category"/> to clear it.
/// </remarks>
public class ItemPatchRequest
{
    /// <summary>New display name. Unique, 1 to 80 characters. Cannot be blank.</summary>
    /// <example>Steel Sword</example>
    [StringLength(80, MinimumLength = 1, ErrorMessage = ItemRules.NameLength)]
    public string? Name { get; set; }

    /// <summary>New description, or an empty string to clear it. Up to 500 characters.</summary>
    /// <example>A blade of hardened steel.</example>
    [StringLength(500, ErrorMessage = ItemRules.DescriptionLength)]
    public string? Description { get; set; }

    /// <summary>New category, or an empty string to clear it. Up to 40 characters.</summary>
    /// <example>Weapon</example>
    [StringLength(40, ErrorMessage = ItemRules.CategoryLength)]
    public string? Category { get; set; }
}

/// <summary>
/// The messages sent back when an item breaks a rule. Kept in one place so that
/// every endpoint words the same problem the same way.
/// </summary>
public static class ItemRules
{
    /// <summary>Sent when the name is missing or blank.</summary>
    public const string NameRequired = "The name is required.";

    /// <summary>Sent when the name is too short or too long.</summary>
    public const string NameLength = "The name must be 1 to 80 characters long.";

    /// <summary>Sent when the description is too long.</summary>
    public const string DescriptionLength = "The description can be up to 500 characters long.";

    /// <summary>Sent when the category is too long.</summary>
    public const string CategoryLength = "The category can be up to 40 characters long.";
}
