using System.ComponentModel.DataAnnotations;
using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Data;
using CraftingPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CraftingPlanner.Api.Controllers;

/// <summary>
/// Endpoints that answer questions about an item's recipe chain, such as what it takes to make it.
/// </summary>
/// <param name="db">The database. Supplied automatically for each request.</param>
[Route("api/items/{id}")]
[Tags("Planning")]
public class PlanningController(PlannerDbContext db) : PlannerControllerBase
{
    /// <summary>The largest quantity a plan can be asked for.</summary>
    public const long MostQuantity = 1_000_000;

    /// <summary>Works out what it takes to make a quantity of an item.</summary>
    /// <remarks>
    /// The answer has three parts: the raw materials to gather, the crafting steps in the order
    /// to do them, and the total time. Each step says how many times to run the recipe and how
    /// many units are left over when a recipe makes more than is needed.
    /// Demand for an ingredient is totalled across every step that needs it before it is rounded,
    /// so a shared ingredient is never over-produced. For a raw item, the plan is just the item itself.
    /// </remarks>
    /// <param name="id">Id of the item to make.</param>
    /// <param name="quantity">How many units to make, from 1 to 1,000,000.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The plan.</returns>
    /// <response code="200">The plan.</response>
    /// <response code="400">The quantity is out of range, the id is not a whole number, or a total is too large to count.</response>
    /// <response code="404">There is no item with that id.</response>
    [HttpGet("plan")]
    [ProducesResponseType<PlanResponse>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<PlanResponse>> Plan(
        int id,
        [FromQuery, Range(1, MostQuantity, ErrorMessage = "The quantity must be 1 to 1,000,000.")] long quantity = 1,
        CancellationToken cancellationToken = default)
    {
        var target = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (target is null)
        {
            return NotFoundRejection("item", id);
        }

        var recipes = await LoadRecipesAsync(cancellationToken);

        PlanResult result;
        try
        {
            result = PlanCalculator.Calculate(id, quantity, recipes);
        }
        catch (PlanTooLargeException tooLarge)
        {
            var itemName = await db.Items
                .Where(i => i.Id == tooLarge.ItemId)
                .Select(i => i.Name)
                .FirstOrDefaultAsync(cancellationToken);

            return Rejection(
                StatusCodes.Status400BadRequest,
                "The plan is too large",
                $"Making {quantity:N0} of \"{target.Name}\" needs more \"{itemName}\" than can be counted. Try a smaller quantity.",
                extraName: "itemId",
                extraValue: tooLarge.ItemId);
        }

        var names = await NamesAsync(
            result.RawMaterials.Select(raw => raw.ItemId).Concat(result.Steps.Select(step => step.ItemId)),
            cancellationToken);

        return new PlanResponse
        {
            ItemId = target.Id,
            ItemName = target.Name,
            Quantity = quantity,
            RawMaterials = result.RawMaterials
                .Select(raw => new RawMaterialLine
                {
                    ItemId = raw.ItemId,
                    ItemName = names[raw.ItemId],
                    Quantity = raw.Quantity,
                })
                .OrderBy(line => line.ItemName)
                .ToList(),
            Steps = result.Steps
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
            TotalSeconds = result.TotalSeconds,
        };
    }

    /// <summary>Loads every recipe in the shape the calculation works on.</summary>
    /// <remarks>
    /// One query for all recipes is simple and, at this data size, fast. A much larger data
    /// set would load only the recipes the target depends on, with a recursive query.
    /// </remarks>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Every recipe, by the id of the item it makes.</returns>
    private async Task<IReadOnlyDictionary<int, PlanRecipe>> LoadRecipesAsync(CancellationToken cancellationToken)
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
    /// <param name="itemIds">The ids to look up. Repeats are fine.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Id to name.</returns>
    private Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> itemIds, CancellationToken cancellationToken)
    {
        var ids = itemIds.Distinct().ToList();

        return db.Items
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);
    }
}
