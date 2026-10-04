using System.ComponentModel.DataAnnotations;
using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Data;
using CraftingPlanner.Api.Models;
using CraftingPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CraftingPlanner.Api.Controllers;

/// <summary>
/// Endpoints that answer questions about an item's recipe chain: what it takes to make it,
/// and what the chain looks like.
/// </summary>
/// <param name="db">The database. Supplied automatically for each request.</param>
[Route("api/items/{id}")]
[Tags("Planning")]
public class PlanningController(PlannerDbContext db) : PlannerControllerBase
{
    /// <summary>The largest quantity a plan can be asked for.</summary>
    public const long MostQuantity = 1_000_000;

    /// <summary>The message sent when the quantity is out of range.</summary>
    public const string QuantityRange = "The quantity must be 1 to 1,000,000.";

    /// <summary>Works out what it takes to make a quantity of an item.</summary>
    /// <remarks>
    /// The answer has three parts: the raw materials to gather, the crafting steps in the order
    /// to do them, and the total time. Each step says how many times to run the recipe and how
    /// many units are left over when a recipe makes more than is needed.
    /// Demand for an ingredient is totalled across every step that needs it before it is rounded,
    /// so a shared ingredient is never over-produced. For a raw item, the plan is just the item itself.
    /// To plan several items together, put them on a list and plan the list.
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
        [FromQuery, Range(1, MostQuantity, ErrorMessage = QuantityRange)] long quantity = 1,
        CancellationToken cancellationToken = default)
    {
        var (target, plan, rejection) = await BuildPlanAsync(id, quantity, cancellationToken);
        if (rejection is not null)
        {
            return rejection;
        }

        return new PlanResponse
        {
            ItemId = target!.Id,
            ItemName = target.Name,
            Quantity = quantity,
            RawMaterials = plan!.RawMaterials,
            Steps = plan.Steps,
            TotalSeconds = plan.TotalSeconds,
        };
    }

    /// <summary>Downloads a plan as a CSV file that opens in a spreadsheet.</summary>
    /// <remarks>
    /// The same plan as <c>GET /api/items/{id}/plan</c>, as one table with a Type column:
    /// the raw materials first, then the steps in crafting order, then the total time.
    /// The response is a file download named after the item and quantity.
    /// </remarks>
    /// <param name="id">Id of the item to make.</param>
    /// <param name="quantity">How many units to make, from 1 to 1,000,000.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The file.</returns>
    /// <response code="200">The plan as a CSV file.</response>
    /// <response code="400">The quantity is out of range, the id is not a whole number, or a total is too large to count.</response>
    /// <response code="404">There is no item with that id.</response>
    [HttpGet("plan/export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> ExportPlan(
        int id,
        [FromQuery, Range(1, MostQuantity, ErrorMessage = QuantityRange)] long quantity = 1,
        CancellationToken cancellationToken = default)
    {
        var (target, plan, rejection) = await BuildPlanAsync(id, quantity, cancellationToken);
        if (rejection is not null)
        {
            return rejection;
        }

        var fileName = $"plan-{FileNames.Slug(target!.Name)}-x{quantity}.csv";

        return File(PlanCsvWriter.WriteBytes(plan!), "text/csv; charset=utf-8", fileName);
    }

    /// <summary>Shows an item's recipe tree: its ingredients, their ingredients, and so on down to raw materials.</summary>
    /// <remarks>
    /// The tree shows structure, not totals. An item that two recipes use appears under each,
    /// and the quantities are per single craft of the item above. For totals, use the plan.
    /// A tree stops growing at 2,000 nodes; any node whose ingredients were left out is marked
    /// <c>truncated</c>, and its own tree can be requested separately.
    /// </remarks>
    /// <param name="id">Id of the item at the top of the tree.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The tree.</returns>
    /// <response code="200">The tree.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">There is no item with that id.</response>
    [HttpGet("tree")]
    [ProducesResponseType<TreeNode>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<TreeNode>> Tree(int id, CancellationToken cancellationToken)
    {
        var target = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (target is null)
        {
            return NotFoundRejection("item", id);
        }

        var recipes = await db.LoadPlanRecipesAsync(cancellationToken);

        // Every item that can appear: the target, and everything any recipe uses or makes.
        var itemIds = recipes.Keys
            .Concat(recipes.Values.SelectMany(recipe => recipe.Ingredients.Select(ingredient => ingredient.ItemId)))
            .Append(id);
        var names = await db.LoadNamesAsync(itemIds, cancellationToken);

        return RecipeTreeBuilder.Build(id, recipes, names);
    }

    /// <summary>Finds the item, runs the calculation, and attaches names.</summary>
    /// <param name="id">Id of the item to make.</param>
    /// <param name="quantity">How many units to make.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The item and its plan, or the rejection to send back.</returns>
    private async Task<(Item? Target, NamedPlan? Plan, ActionResult? Rejection)> BuildPlanAsync(
        int id,
        long quantity,
        CancellationToken cancellationToken)
    {
        var target = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (target is null)
        {
            return (null, null, NotFoundRejection("item", id));
        }

        try
        {
            var plan = await db.AssemblePlanAsync([new PlanTarget(id, quantity)], cancellationToken);
            return (target, plan, null);
        }
        catch (PlanTooLargeException tooLarge)
        {
            return (target, null, await TooLargeRejectionAsync(db, this, $"{quantity:N0} of \"{target.Name}\"", tooLarge, cancellationToken));
        }
    }

    /// <summary>Builds the 400 rejection for a plan whose totals do not fit in 64 bits.</summary>
    /// <param name="db">The database, to look up the item's name.</param>
    /// <param name="controller">The controller answering, which builds the rejection.</param>
    /// <param name="what">What was asked for, such as "3 of "Tool Kit"" or "the list "Starter gear"".</param>
    /// <param name="tooLarge">The error from the calculation.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The response to send back.</returns>
    public static async Task<ObjectResult> TooLargeRejectionAsync(
        PlannerDbContext db,
        PlannerControllerBase controller,
        string what,
        PlanTooLargeException tooLarge,
        CancellationToken cancellationToken)
    {
        var itemName = await db.Items
            .Where(i => i.Id == tooLarge.ItemId)
            .Select(i => i.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return controller.TooLarge(
            $"Making {what} needs more \"{itemName}\" than can be counted. Try a smaller quantity.",
            tooLarge.ItemId);
    }
}
