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

    /// <summary>Optional name of the item's picture, one of the names listed by <c>GET /api/icons</c>.</summary>
    /// <example>metal-bar</example>
    [StringLength(40, ErrorMessage = ItemRules.IconLength)]
    [RegularExpression(ItemRules.IconPattern, ErrorMessage = ItemRules.IconFormat)]
    public string? Icon { get; set; }
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

    /// <summary>New picture name, or an empty string to clear it. One of the names listed by <c>GET /api/icons</c>.</summary>
    /// <example>two-handed-sword</example>
    [StringLength(40, ErrorMessage = ItemRules.IconLength)]
    [RegularExpression(ItemRules.IconPattern, ErrorMessage = ItemRules.IconFormat)]
    public string? Icon { get; set; }
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

    /// <summary>Sent when the icon name is too long.</summary>
    public const string IconLength = "The icon name can be up to 40 characters long.";

    /// <summary>What an icon name may contain: lower-case letters, digits, and hyphens. An empty string clears it.</summary>
    public const string IconPattern = "^[a-z0-9-]*$";

    /// <summary>Sent when the icon name has other characters in it.</summary>
    public const string IconFormat = "The icon name can only contain lower-case letters, digits, and hyphens.";

    /// <summary>Sent when the icon name is not one the website ships with. The endpoint adds the name.</summary>
    public const string IconUnknown = "There is no icon with that name. GET /api/icons lists the icons.";
}
