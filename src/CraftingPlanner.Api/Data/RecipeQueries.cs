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
