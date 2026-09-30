using CraftingPlanner.Api.Services;

namespace CraftingPlanner.Tests;

/// <summary>
/// The recipes used by the calculation tests: the worked example from the design document,
/// plus Torch, Lantern, and Camp Kit, which use one item at two different depths.
/// These are the same recipes as in <c>prototype/test_plan.py</c>.
/// </summary>
/// <remarks>
/// The calculation works with item ids, but ids make tests hard to read, so the tests
/// use names and this class translates. An item's id is its position in <see cref="Names"/>,
/// counting from 1.
/// </remarks>
public static class SampleRecipes
{
    /// <summary>Every item, raw and crafted. The position in this list, counting from 1, is the item's id.</summary>
    public static readonly string[] Names =
    [
        "Log", "Iron Ore", "Coal",
        "Plank", "Stick", "Iron Ingot", "Sword", "Pickaxe", "Tool Kit",
        "Torch", "Lantern", "Camp Kit",
    ];

    /// <summary>The recipes, by the id of the item each one makes.</summary>
    public static readonly IReadOnlyDictionary<int, PlanRecipe> Recipes = new Dictionary<int, PlanRecipe>
    {
        [Id("Plank")] = Recipe(makes: 2, seconds: 2, ("Log", 1)),
        [Id("Stick")] = Recipe(makes: 4, seconds: 2, ("Plank", 2)),
        [Id("Iron Ingot")] = Recipe(makes: 1, seconds: 10, ("Iron Ore", 2), ("Coal", 1)),
        [Id("Sword")] = Recipe(makes: 1, seconds: 8, ("Iron Ingot", 2), ("Stick", 1)),
        [Id("Pickaxe")] = Recipe(makes: 1, seconds: 8, ("Iron Ingot", 3), ("Stick", 2)),
        [Id("Tool Kit")] = Recipe(makes: 1, seconds: 4, ("Sword", 1), ("Pickaxe", 1)),
        [Id("Torch")] = Recipe(makes: 4, seconds: 1, ("Coal", 1), ("Stick", 1)),
        [Id("Lantern")] = Recipe(makes: 1, seconds: 5, ("Torch", 1), ("Iron Ingot", 1)),
        [Id("Camp Kit")] = Recipe(makes: 1, seconds: 3, ("Lantern", 2), ("Torch", 3)),
    };

    /// <summary>The id of a named item.</summary>
    /// <param name="name">The item's name, as listed in <see cref="Names"/>.</param>
    /// <returns>The id.</returns>
    public static int Id(string name)
    {
        var position = Array.IndexOf(Names, name);
        return position >= 0
            ? position + 1
            : throw new ArgumentException($"There is no sample item named \"{name}\".", nameof(name));
    }

    /// <summary>The name of an item with a given id.</summary>
    /// <param name="id">The item's id.</param>
    /// <returns>The name.</returns>
    public static string Name(int id) => Names[id - 1];

    /// <summary>Works out a plan using the sample recipes.</summary>
    /// <param name="target">Name of the item to make.</param>
    /// <param name="quantity">How many to make.</param>
    /// <returns>The plan.</returns>
    public static PlanResult Plan(string target, long quantity) =>
        PlanCalculator.Calculate(Id(target), quantity, Recipes);

    /// <summary>Builds a recipe from item names.</summary>
    /// <param name="makes">Units one craft produces.</param>
    /// <param name="seconds">Time for one craft.</param>
    /// <param name="ingredients">Each ingredient's name and the units one craft uses.</param>
    /// <returns>The recipe.</returns>
    private static PlanRecipe Recipe(int makes, int seconds, params (string Name, int Quantity)[] ingredients) =>
        new(
            makes,
            seconds,
            ingredients.Select(ingredient => new PlanIngredient(Id(ingredient.Name), ingredient.Quantity)).ToList());
}
