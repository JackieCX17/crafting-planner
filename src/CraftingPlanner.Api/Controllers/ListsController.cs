using System.ComponentModel.DataAnnotations;
using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Data;
using CraftingPlanner.Api.Models;
using CraftingPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CraftingPlanner.Api.Controllers;

/// <summary>
/// Endpoints for shopping lists: named lists of items to make, each with a quantity,
/// that can be planned together.
/// </summary>
/// <param name="db">The database. Supplied automatically for each request.</param>
[Route("api/lists")]
[Tags("Lists")]
public class ListsController(PlannerDbContext db) : PlannerControllerBase
{
    /// <summary>Lists the shopping lists.</summary>
    /// <param name="page">The page to return. The first page is 1.</param>
    /// <param name="pageSize">How many lists a page holds, from 1 to 100.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>One page of lists, in name order, with the total number.</returns>
    /// <response code="200">The lists. The page is empty when there are none.</response>
    /// <response code="400">A paging value is not valid.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<ListSummary>>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public async Task<ActionResult<PagedResult<ListSummary>>> List(
        [FromQuery, Range(1, 100_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await db.ShoppingLists.CountAsync(cancellationToken);

        var pageOfLists = await db.ShoppingLists
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new ListSummary
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                EntryCount = l.Entries.Count,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ListSummary>
        {
            Items = pageOfLists,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>Gets one shopping list with everything on it.</summary>
    /// <param name="id">Id of the list.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The list.</returns>
    /// <response code="200">The list.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">There is no list with that id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType<ListDetail>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<ListDetail>> Get(int id, CancellationToken cancellationToken)
    {
        var list = await FindWithEntriesAsync(id, cancellationToken);

        return list is null
            ? NotFoundRejection("list", id)
            : ToDetail(list);
    }

    /// <summary>Creates an empty shopping list.</summary>
    /// <param name="request">The list's name and note.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The list as saved, including its new id.</returns>
    /// <response code="201">The list was created. The Location header holds its address.</response>
    /// <response code="400">A field is missing or not valid.</response>
    /// <response code="409">Another list already has that name.</response>
    [HttpPost]
    [ProducesResponseType<ListDetail>(StatusCodes.Status201Created, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<ListDetail>> Create(ListRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await NameIsTakenAsync(name, exceptId: null, cancellationToken))
        {
            return NameTakenRejection(name);
        }

        var list = new ShoppingList { Name = name, Description = Tidy(request.Description) };
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = list.Id }, ToDetail(list));
    }

    /// <summary>Renames a shopping list or changes its note. The items on it are not affected.</summary>
    /// <param name="id">Id of the list.</param>
    /// <param name="request">The new name and note.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The list as saved.</returns>
    /// <response code="200">The list was changed.</response>
    /// <response code="400">A field is missing or not valid, or the id is not a whole number.</response>
    /// <response code="404">There is no list with that id.</response>
    /// <response code="409">Another list already has that name.</response>
    [HttpPut("{id}")]
    [ProducesResponseType<ListDetail>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<ListDetail>> Rename(int id, ListRequest request, CancellationToken cancellationToken)
    {
        var list = await db.ShoppingLists.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (list is null)
        {
            return NotFoundRejection("list", id);
        }

        var name = request.Name.Trim();
        if (await NameIsTakenAsync(name, exceptId: id, cancellationToken))
        {
            return NameTakenRejection(name);
        }

        list.Name = name;
        list.Description = Tidy(request.Description);
        await db.SaveChangesAsync(cancellationToken);

        return ToDetail((await FindWithEntriesAsync(id, cancellationToken))!);
    }

    /// <summary>Deletes a shopping list. The items on it are not affected.</summary>
    /// <param name="id">Id of the list.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Nothing.</returns>
    /// <response code="204">The list was deleted.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">There is no list with that id.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var list = await db.ShoppingLists.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (list is null)
        {
            return NotFoundRejection("list", id);
        }

        db.ShoppingLists.Remove(list);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Puts an item on a shopping list, or changes how many of it the list asks for.</summary>
    /// <remarks>
    /// Sending the same request twice gives the same result as sending it once. A list holds
    /// at most 50 different items.
    /// </remarks>
    /// <param name="id">Id of the list.</param>
    /// <param name="itemId">Id of the item to make.</param>
    /// <param name="request">How many units to make.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The entry as saved.</returns>
    /// <response code="200">The quantity of an item already on the list was changed.</response>
    /// <response code="201">The item was added to the list.</response>
    /// <response code="400">The quantity is out of range, or an id is not a whole number.</response>
    /// <response code="404">There is no list with that id, or no item with that id.</response>
    /// <response code="409">The list already holds 50 items.</response>
    [HttpPut("{id}/items/{itemId}")]
    [Tags("List entries")]
    [ProducesResponseType<ListEntryLine>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ListEntryLine>(StatusCodes.Status201Created, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<ListEntryLine>> SetEntry(
        int id,
        int itemId,
        ListEntryRequest request,
        CancellationToken cancellationToken)
    {
        var list = await db.ShoppingLists.Include(l => l.Entries).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (list is null)
        {
            return NotFoundRejection("list", id);
        }

        var item = await db.Items.AsNoTracking().Include(i => i.Recipe).FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null)
        {
            return NotFoundRejection("item", itemId);
        }

        var saved = new ListEntryLine
        {
            ItemId = item.Id,
            ItemName = item.Name,
            Category = item.Category,
            Icon = item.Icon,
            Kind = item.Recipe is null ? ItemKind.Raw : ItemKind.Crafted,
            Quantity = request.Quantity,
        };

        var existing = list.Entries.FirstOrDefault(entry => entry.ItemId == itemId);
        if (existing is not null)
        {
            existing.Quantity = request.Quantity;
            await db.SaveChangesAsync(cancellationToken);

            return saved;
        }

        if (list.Entries.Count >= ListRules.MostEntries)
        {
            return Rejection(
                StatusCodes.Status409Conflict,
                "The list is full",
                $"\"{list.Name}\" already holds {ListRules.MostEntries} items, which is the most a list can have.");
        }

        list.Entries.Add(new ShoppingListEntry { ItemId = itemId, Quantity = request.Quantity });
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id }, saved);
    }

    /// <summary>Takes an item off a shopping list.</summary>
    /// <param name="id">Id of the list.</param>
    /// <param name="itemId">Id of the item.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Nothing.</returns>
    /// <response code="204">The item was taken off the list.</response>
    /// <response code="400">An id is not a whole number.</response>
    /// <response code="404">There is no list with that id, or the item is not on it.</response>
    [HttpDelete("{id}/items/{itemId}")]
    [Tags("List entries")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> RemoveEntry(int id, int itemId, CancellationToken cancellationToken)
    {
        var list = await db.ShoppingLists.Include(l => l.Entries).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (list is null)
        {
            return NotFoundRejection("list", id);
        }

        var entry = list.Entries.FirstOrDefault(e => e.ItemId == itemId);
        if (entry is null)
        {
            return Rejection(
                StatusCodes.Status404NotFound,
                "The item is not on the list",
                $"List {id} does not have item {itemId} on it.");
        }

        list.Entries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Works out what it takes to make everything on a shopping list.</summary>
    /// <remarks>
    /// The same answer as planning one item, for all the list's entries together: raw materials,
    /// crafting steps in order, and the total time. Demand is totalled across the whole list before
    /// rounding, so an ingredient two entries share is never over-produced, and an entry that
    /// another entry needs is counted once with both shares added. An empty list gives an empty plan.
    /// </remarks>
    /// <param name="id">Id of the list.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The plan.</returns>
    /// <response code="200">The plan.</response>
    /// <response code="400">The id is not a whole number, or a total is too large to count.</response>
    /// <response code="404">There is no list with that id.</response>
    [HttpGet("{id}/plan")]
    [Tags("Planning")]
    [ProducesResponseType<ListPlanResponse>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<ListPlanResponse>> Plan(int id, CancellationToken cancellationToken)
    {
        var (list, plan, rejection) = await BuildPlanAsync(id, cancellationToken);
        if (rejection is not null)
        {
            return rejection;
        }

        return new ListPlanResponse
        {
            ListId = list!.Id,
            ListName = list.Name,
            Targets = ToDetail(list).Entries,
            RawMaterials = plan!.RawMaterials,
            Steps = plan.Steps,
            TotalSeconds = plan.TotalSeconds,
        };
    }

    /// <summary>Downloads a shopping list's plan as a CSV file that opens in a spreadsheet.</summary>
    /// <remarks>The same layout as the single-item export, named after the list.</remarks>
    /// <param name="id">Id of the list.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The file.</returns>
    /// <response code="200">The plan as a CSV file.</response>
    /// <response code="400">The id is not a whole number, or a total is too large to count.</response>
    /// <response code="404">There is no list with that id.</response>
    [HttpGet("{id}/plan/export")]
    [Tags("Planning")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> ExportPlan(int id, CancellationToken cancellationToken)
    {
        var (list, plan, rejection) = await BuildPlanAsync(id, cancellationToken);
        if (rejection is not null)
        {
            return rejection;
        }

        return File(PlanCsvWriter.WriteBytes(plan!), "text/csv; charset=utf-8", $"list-{FileNames.Slug(list!.Name)}.csv");
    }

    /// <summary>Finds the list, runs the calculation for its entries, and attaches names.</summary>
    /// <param name="id">Id of the list.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The list and its plan, or the rejection to send back.</returns>
    private async Task<(ShoppingList? List, NamedPlan? Plan, ActionResult? Rejection)> BuildPlanAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var list = await FindWithEntriesAsync(id, cancellationToken);
        if (list is null)
        {
            return (null, null, NotFoundRejection("list", id));
        }

        var targets = list.Entries.Select(entry => new PlanTarget(entry.ItemId, entry.Quantity)).ToList();

        try
        {
            return (list, await db.AssemblePlanAsync(targets, cancellationToken), null);
        }
        catch (PlanTooLargeException tooLarge)
        {
            return (list, null, await PlanningController.TooLargeRejectionAsync(db, this, $"the list \"{list.Name}\"", tooLarge, cancellationToken));
        }
    }

    /// <summary>Checks whether a name is already used by another list.</summary>
    /// <param name="name">The name to check. Upper and lower case are treated the same.</param>
    /// <param name="exceptId">Id of a list to leave out of the check.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>True when another list has the name.</returns>
    private Task<bool> NameIsTakenAsync(string name, int? exceptId, CancellationToken cancellationToken) =>
        db.ShoppingLists.AnyAsync(l => l.Id != exceptId && l.Name == name, cancellationToken);

    /// <summary>Builds the 409 rejection for a name that is already in use.</summary>
    /// <param name="name">The name that was asked for.</param>
    /// <returns>The response to send back.</returns>
    private ObjectResult NameTakenRejection(string name) =>
        Rejection(
            StatusCodes.Status409Conflict,
            "The name is already in use",
            $"Another list is already named \"{name}\". List names must be unique.");

    /// <summary>Loads a list for reading, with its entries and their items.</summary>
    /// <param name="id">Id of the list.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The list, or null when there is none with that id.</returns>
    private Task<ShoppingList?> FindWithEntriesAsync(int id, CancellationToken cancellationToken) =>
        db.ShoppingLists
            .AsNoTracking()
            .Include(l => l.Entries)
                .ThenInclude(entry => entry.Item)
                .ThenInclude(item => item.Recipe)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    /// <summary>Converts a database row into the shape the API returns.</summary>
    /// <param name="list">The list, loaded with its entries and their items.</param>
    /// <returns>The list as the API returns it.</returns>
    private static ListDetail ToDetail(ShoppingList list) =>
        new()
        {
            Id = list.Id,
            Name = list.Name,
            Description = list.Description,
            EntryCount = list.Entries.Count,
            Entries = list.Entries
                .OrderBy(entry => entry.Item.Name)
                .Select(entry => new ListEntryLine
                {
                    ItemId = entry.ItemId,
                    ItemName = entry.Item.Name,
                    Category = entry.Item.Category,
                    Icon = entry.Item.Icon,
                    Kind = entry.Item.Recipe is null ? ItemKind.Raw : ItemKind.Crafted,
                    Quantity = entry.Quantity,
                })
                .ToList(),
        };
}
