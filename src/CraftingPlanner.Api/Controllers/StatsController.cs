using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Data;
using CraftingPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CraftingPlanner.Api.Controllers;

/// <summary>Endpoints that summarise the data as a whole.</summary>
/// <param name="db">The database. Supplied automatically for each request.</param>
[Route("api/stats")]
[Tags("Summary")]
public class StatsController(PlannerDbContext db) : PlannerControllerBase
{
    /// <summary>Totals and highlights across all the data.</summary>
    /// <remarks>
    /// Counts of items, recipes, and categories, plus the most used ingredient and the item
    /// with the longest chain of recipes beneath it. Ties are settled by name.
    /// </remarks>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The summary.</returns>
    /// <response code="200">The summary.</response>
    [HttpGet]
    [ProducesResponseType<StatsResponse>(StatusCodes.Status200OK, Json)]
    public async Task<ActionResult<StatsResponse>> Get(CancellationToken cancellationToken)
    {
        var itemCount = await db.Items.CountAsync(cancellationToken);
        var craftedCount = await db.Recipes.CountAsync(cancellationToken);
        var categoryCount = await db.Items
            .Where(i => i.Category != null)
            .Select(i => i.Category)
            .Distinct()
            .CountAsync(cancellationToken);
        var lineCount = await db.RecipeIngredients.CountAsync(cancellationToken);

        var mostUsed = await db.RecipeIngredients
            .GroupBy(line => new { line.ItemId, line.Item.Name })
            .Select(group => new IngredientUse
            {
                ItemId = group.Key.ItemId,
                ItemName = group.Key.Name,
                RecipeCount = group.Count(),
            })
            .OrderByDescending(use => use.RecipeCount)
            .ThenBy(use => use.ItemName)
            .FirstOrDefaultAsync(cancellationToken);

        return new StatsResponse
        {
            ItemCount = itemCount,
            RawItemCount = itemCount - craftedCount,
            CraftedItemCount = craftedCount,
            CategoryCount = categoryCount,
            IngredientLineCount = lineCount,
            MostUsedIngredient = mostUsed,
            LongestChain = await LongestChainAsync(cancellationToken),
        };
    }

    /// <summary>Finds the crafted item with the longest chain of recipes beneath it.</summary>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The item and its depth, or null when there are no recipes.</returns>
    private async Task<ChainLength?> LongestChainAsync(CancellationToken cancellationToken)
    {
        var recipes = await db.LoadPlanRecipesAsync(cancellationToken);
        if (recipes.Count == 0)
        {
            return null;
        }

        var depths = RecipeDepths.Compute(recipes);
        var deepest = depths.Values.Max();
        var candidates = depths.Where(pair => pair.Value == deepest).Select(pair => pair.Key).ToList();
        var names = await db.LoadNamesAsync(candidates, cancellationToken);

        var winner = candidates.OrderBy(itemId => names[itemId]).First();

        return new ChainLength { ItemId = winner, ItemName = names[winner], Depth = deepest };
    }
}
