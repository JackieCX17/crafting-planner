using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CraftingPlanner.Api.Data;

/// <summary>
/// Queries that several endpoints share, written as extra methods on the database connection.
/// </summary>
public static class RecipeQueries
{
    /// <summary>Loads every recipe in the plain shape the calculations work on.</summary>
    /// <remarks>
    /// One query for all recipes is simple and, at this data size, fast. A much larger data
    /// set would load only the recipes an item depends on, with a recursive query.
    /// </remarks>
    /// <param name="db">The database.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Every recipe, by the id of the item it makes.</returns>
    public static async Task<IReadOnlyDictionary<int, PlanRecipe>> LoadPlanRecipesAsync(
        this PlannerDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.Recipes
            .AsNoTracking()
            .Select(r => new
            {
                r.OutputItemId,
                r.OutputQuantity,
                r.CraftSeconds,
                Ingredients = r.Ingredients.Select(line => new { line.ItemId, line.Quantity }).ToList(),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => row.OutputItemId,
            row => new PlanRecipe(
                row.OutputQuantity,
                row.CraftSeconds,
                row.Ingredients.Select(line => new PlanIngredient(line.ItemId, line.Quantity)).ToList()));
    }

    /// <summary>
    /// Loads the recipes, runs the planning calculation for one or more targets, and attaches
    /// item names. Shared by the single-item plan and the list plan, so they cannot disagree.
    /// </summary>
    /// <param name="db">The database.</param>
    /// <param name="targets">The items to make, each with a quantity.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The plan with names, raw materials in name order.</returns>
    /// <exception cref="PlanTooLargeException">A total would not fit in a 64-bit whole number.</exception>
    public static async Task<NamedPlan> AssemblePlanAsync(
        this PlannerDbContext db,
        IReadOnlyList<PlanTarget> targets,
        CancellationToken cancellationToken)
    {
        var recipes = await db.LoadPlanRecipesAsync(cancellationToken);
        var result = PlanCalculator.Calculate(targets, recipes);

        var names = await db.LoadNamesAsync(
            result.RawMaterials.Select(raw => raw.ItemId).Concat(result.Steps.Select(step => step.ItemId)),
            cancellationToken);

        return new NamedPlan(
            result.RawMaterials
                .Select(raw => new RawMaterialLine { ItemId = raw.ItemId, ItemName = names[raw.ItemId], Quantity = raw.Quantity })
                .OrderBy(line => line.ItemName)
                .ToList(),
            result.Steps
                .Select(step => new PlanStepLine
                {
                    ItemId = step.ItemId,
                    ItemName = names[step.ItemId],
                    Needed = step.Needed,
                    Crafts = step.Crafts,
                    Made = step.Made,
                    Leftover = step.Leftover,
                    Seconds = step.Seconds,
                })
                .ToList(),
            result.TotalSeconds);
    }

    /// <summary>Looks up the names of a set of items.</summary>
    /// <param name="db">The database.</param>
    /// <param name="itemIds">The ids to look up. Repeats are fine.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Id to name.</returns>
    public static Task<Dictionary<int, string>> LoadNamesAsync(
        this PlannerDbContext db,
        IEnumerable<int> itemIds,
        CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToList();

        return db.Items
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);
    }
}
