using System.ComponentModel.DataAnnotations;
using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Data;
using CraftingPlanner.Api.Models;
using CraftingPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CraftingPlanner.Api.Controllers;

/// <summary>
/// Endpoints for items: the things that can be crafted or used as ingredients.
/// </summary>
/// <param name="db">The database. Supplied automatically for each request.</param>
/// <param name="icons">The pictures an item can be given. Supplied automatically.</param>
[Route("api/items")]
[Tags("Items")]
public class ItemsController(PlannerDbContext db, IconCatalog icons) : PlannerControllerBase
{
    /// <summary>Lists items.</summary>
    /// <remarks>
    /// Every filter is optional, and filters combine. For example, setting <c>kind</c>
    /// to <c>crafted</c> and <c>search</c> to <c>iron</c> returns crafted items with
    /// "iron" in their name or description.
    /// </remarks>
    /// <param name="search">Text to look for in the name or description. Upper and lower case are treated the same.</param>
    /// <param name="category">Return only items in this category.</param>
    /// <param name="kind">Return only raw items, or only crafted items.</param>
    /// <param name="sort">The field to sort by.</param>
    /// <param name="order">The direction to sort in.</param>
    /// <param name="page">The page to return. The first page is 1.</param>
    /// <param name="pageSize">How many items a page holds, from 1 to 100.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>One page of matching items, with the total number that matched.</returns>
    /// <response code="200">The matching items. The list is empty when nothing matched.</response>
    /// <response code="400">A filter, sort, or paging value is not valid.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<ItemSummary>>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public async Task<ActionResult<PagedResult<ItemSummary>>> List(
        [FromQuery, StringLength(80)] string? search = null,
        [FromQuery, StringLength(40)] string? category = null,
        [FromQuery] ItemKind? kind = null,
        [FromQuery] ItemSort sort = ItemSort.Name,
        [FromQuery] SortOrder order = SortOrder.Asc,
        [FromQuery, Range(1, 100_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Item> items = db.Items.AsNoTracking();

        if (Tidy(search) is { } searchText)
        {
            var pattern = $"%{EscapeForLike(searchText)}%";
            items = items.Where(i =>
                EF.Functions.Like(i.Name, pattern, LikeEscape)
                || (i.Description != null && EF.Functions.Like(i.Description, pattern, LikeEscape)));
        }

        if (Tidy(category) is { } categoryText)
        {
            items = items.Where(i => i.Category == categoryText);
        }

        items = kind switch
        {
            ItemKind.Raw => items.Where(i => i.Recipe == null),
            ItemKind.Crafted => items.Where(i => i.Recipe != null),
            _ => items,
        };

        var totalCount = await items.CountAsync(cancellationToken);

        // Every sort ends with the id, so the order is always the same
        // and an item cannot appear on two pages.
        items = (sort, order) switch
        {
            (ItemSort.Name, SortOrder.Asc) => items.OrderBy(i => i.Name).ThenBy(i => i.Id),
            (ItemSort.Name, SortOrder.Desc) => items.OrderByDescending(i => i.Name).ThenBy(i => i.Id),
            (ItemSort.Category, SortOrder.Asc) => items.OrderBy(i => i.Category).ThenBy(i => i.Name).ThenBy(i => i.Id),
            (ItemSort.Category, SortOrder.Desc) => items.OrderByDescending(i => i.Category).ThenBy(i => i.Name).ThenBy(i => i.Id),
            (_, SortOrder.Desc) => items.OrderByDescending(i => i.Id),
            _ => items.OrderBy(i => i.Id),
        };

        var pageOfItems = await items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ItemSummary
            {
                Id = i.Id,
                Name = i.Name,
                Description = i.Description,
                Category = i.Category,
                Icon = i.Icon,
                Kind = i.Recipe == null ? ItemKind.Raw : ItemKind.Crafted,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ItemSummary>
        {
            Items = pageOfItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>Gets one item.</summary>
    /// <remarks>
    /// The response includes the recipe that makes the item and the items it is used to make,
    /// so one request answers both "what goes into this?" and "what does this go into?".
    /// </remarks>
    /// <param name="id">Id of the item.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The item.</returns>
    /// <response code="200">The item.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">There is no item with that id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType<ItemDetail>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<ItemDetail>> Get(int id, CancellationToken cancellationToken)
    {
        var item = await FindWithLinksAsync(id, cancellationToken);

        return item is null
            ? NotFoundRejection("item", id)
            : ToDetail(item);
    }

    /// <summary>Creates an item.</summary>
    /// <remarks>
    /// A new item is raw. It becomes crafted when a recipe is created for it.
    /// </remarks>
    /// <param name="request">The new item's fields.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The item as saved, including its new id.</returns>
    /// <response code="201">The item was created. The Location header holds its address.</response>
    /// <response code="400">A field is missing or not valid, or the icon is not one the website ships with.</response>
    /// <response code="409">Another item already has that name.</response>
    [HttpPost]
    [ProducesResponseType<ItemDetail>(StatusCodes.Status201Created, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<ItemDetail>> Create(ItemRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await NameIsTakenAsync(name, exceptId: null, cancellationToken))
        {
            return NameTakenRejection(name);
        }

        if (IconIsUnknown(request.Icon))
        {
            return ValidationProblem(ModelState);
        }

        var item = new Item
        {
            Name = name,
            Description = Tidy(request.Description),
            Category = Tidy(request.Category),
            Icon = Tidy(request.Icon),
        };

        db.Items.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = item.Id }, ToDetail(item));
    }

    /// <summary>Replaces every field of an item.</summary>
    /// <remarks>
    /// An optional field that is left out is cleared. To change one field and keep
    /// the rest, use PATCH.
    /// </remarks>
    /// <param name="id">Id of the item.</param>
    /// <param name="request">The item's new fields.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The item as saved.</returns>
    /// <response code="200">The item was replaced.</response>
    /// <response code="400">A field is missing or not valid, or the id is not a whole number.</response>
    /// <response code="404">There is no item with that id.</response>
    /// <response code="409">Another item already has that name.</response>
    [HttpPut("{id}")]
    [ProducesResponseType<ItemDetail>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<ItemDetail>> Replace(
        int id,
        ItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFoundRejection("item", id);
        }

        var name = request.Name.Trim();
        if (await NameIsTakenAsync(name, exceptId: id, cancellationToken))
        {
            return NameTakenRejection(name);
        }

        if (IconIsUnknown(request.Icon))
        {
            return ValidationProblem(ModelState);
        }

        item.Name = name;
        item.Description = Tidy(request.Description);
        item.Category = Tidy(request.Category);
        item.Icon = Tidy(request.Icon);
        await db.SaveChangesAsync(cancellationToken);

        return await SavedItemAsync(id, cancellationToken);
    }

    /// <summary>Changes part of an item.</summary>
    /// <remarks>
    /// Only the fields that are sent are changed. Send an empty string for the description
    /// or category to clear it. A request with no fields changes nothing and returns the item as it is.
    /// </remarks>
    /// <param name="id">Id of the item.</param>
    /// <param name="request">The fields to change.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The item as saved.</returns>
    /// <response code="200">The item was changed.</response>
    /// <response code="400">A field is not valid, the name is blank, or the id is not a whole number.</response>
    /// <response code="404">There is no item with that id.</response>
    /// <response code="409">Another item already has that name.</response>
    [HttpPatch("{id}")]
    [ProducesResponseType<ItemDetail>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<ItemDetail>> Change(
        int id,
        ItemPatchRequest request,
        CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFoundRejection("item", id);
        }

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
            {
                ModelState.AddModelError("name", ItemRules.NameRequired);
                return ValidationProblem(ModelState);
            }

            if (await NameIsTakenAsync(name, exceptId: id, cancellationToken))
            {
                return NameTakenRejection(name);
            }

            item.Name = name;
        }

        if (request.Description is not null)
        {
            item.Description = Tidy(request.Description);
        }

        if (request.Category is not null)
        {
            item.Category = Tidy(request.Category);
        }

        if (request.Icon is not null)
        {
            if (IconIsUnknown(request.Icon))
            {
                return ValidationProblem(ModelState);
            }

            item.Icon = Tidy(request.Icon);
        }

        await db.SaveChangesAsync(cancellationToken);

        return await SavedItemAsync(id, cancellationToken);
    }

    /// <summary>Deletes an item.</summary>
    /// <remarks>
    /// The item's own recipe is deleted with it, and it is removed from any shopping lists
    /// it is on. An item that other recipes use as an ingredient cannot be deleted: remove it
    /// from those recipes first. The rejection lists them in its <c>usedIn</c> field.
    /// </remarks>
    /// <param name="id">Id of the item.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Nothing.</returns>
    /// <response code="204">The item was deleted.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">There is no item with that id.</response>
    /// <response code="409">Other recipes use the item as an ingredient.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFoundRejection("item", id);
        }

        var usedIn = await db.RecipeIngredients
            .Where(line => line.ItemId == id)
            .OrderBy(line => line.Recipe.OutputItem.Name)
            .Select(line => new ItemUse
            {
                RecipeId = line.RecipeId,
                ItemId = line.Recipe.OutputItemId,
                ItemName = line.Recipe.OutputItem.Name,
                Quantity = line.Quantity,
            })
            .ToListAsync(cancellationToken);

        if (usedIn.Count > 0)
        {
            var names = string.Join(", ", usedIn.Select(use => use.ItemName));
            return Rejection(
                StatusCodes.Status409Conflict,
                "The item is in use",
                $"\"{item.Name}\" is an ingredient of: {names}. Remove it from those recipes first.",
                extraName: "usedIn",
                extraValue: usedIn);
        }

        db.Items.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>The character that marks a wildcard as ordinary text in a search pattern.</summary>
    private const string LikeEscape = "\\";

    /// <summary>
    /// Makes search text safe to place inside a LIKE pattern. In a pattern, % and _ are
    /// wildcards, so a search for "50%" would otherwise match far more than intended.
    /// </summary>
    /// <param name="text">The text being searched for.</param>
    /// <returns>The text with its wildcard characters marked as ordinary text.</returns>
    private static string EscapeForLike(string text) =>
        text.Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");

    /// <summary>
    /// Checks whether an icon name refers to a picture the website does not have, and if so
    /// records the problem against the icon field. Blank means "no icon" and is always fine.
    /// </summary>
    /// <param name="icon">The icon name that was sent.</param>
    /// <returns>True when the icon is unknown.</returns>
    private bool IconIsUnknown(string? icon)
    {
        if (Tidy(icon) is not { } name || icons.Contains(name))
        {
            return false;
        }

        ModelState.AddModelError("icon", ItemRules.IconUnknown);
        return true;
    }

    /// <summary>Checks whether a name is already used by another item.</summary>
    /// <param name="name">The name to check. Upper and lower case are treated the same.</param>
    /// <param name="exceptId">Id of an item to leave out of the check, so an item does not clash with itself.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>True when another item has the name.</returns>
    private Task<bool> NameIsTakenAsync(string name, int? exceptId, CancellationToken cancellationToken) =>
        db.Items.AnyAsync(i => i.Id != exceptId && i.Name == name, cancellationToken);

    /// <summary>Builds the 409 rejection for a name that is already in use.</summary>
    /// <param name="name">The name that was asked for.</param>
    /// <returns>The response to send back.</returns>
    private ObjectResult NameTakenRejection(string name) =>
        Rejection(
            StatusCodes.Status409Conflict,
            "The name is already in use",
            $"Another item is already named \"{name}\". Item names must be unique.");

    /// <summary>Loads an item together with its recipe and the recipes that use it.</summary>
    /// <param name="id">Id of the item.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The item, or null when there is none with that id.</returns>
    private Task<Item?> FindWithLinksAsync(int id, CancellationToken cancellationToken) =>
        db.Items
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.Recipe!)
                .ThenInclude(r => r.Ingredients)
                .ThenInclude(line => line.Item)
            .Include(i => i.UsedIn)
                .ThenInclude(line => line.Recipe)
                .ThenInclude(r => r.OutputItem)
            .Include(i => i.OnLists)
                .ThenInclude(entry => entry.List)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <summary>Reloads an item that was just saved and returns it to the caller.</summary>
    /// <param name="id">Id of the item.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The item as it now is in the database.</returns>
    private async Task<ActionResult<ItemDetail>> SavedItemAsync(int id, CancellationToken cancellationToken)
    {
        var saved = await FindWithLinksAsync(id, cancellationToken);

        return saved is null
            ? NotFoundRejection("item", id)
            : ToDetail(saved);
    }

    /// <summary>Converts a database row into the shape the API returns.</summary>
    /// <param name="item">The item, loaded with its recipe and uses.</param>
    /// <returns>The item as the API returns it.</returns>
    private static ItemDetail ToDetail(Item item) =>
        new()
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Category = item.Category,
            Icon = item.Icon,
            Kind = item.Recipe is null ? ItemKind.Raw : ItemKind.Crafted,
            Recipe = item.Recipe is null
                ? null
                : new RecipeBrief
                {
                    Id = item.Recipe.Id,
                    OutputQuantity = item.Recipe.OutputQuantity,
                    CraftSeconds = item.Recipe.CraftSeconds,
                    Ingredients = item.Recipe.Ingredients
                        .OrderBy(line => line.Item.Name)
                        .Select(line => new IngredientLine
                        {
                            ItemId = line.ItemId,
                            ItemName = line.Item.Name,
                            Quantity = line.Quantity,
                        })
                        .ToList(),
                },
            UsedIn = item.UsedIn
                .OrderBy(line => line.Recipe.OutputItem.Name)
                .Select(line => new ItemUse
                {
                    RecipeId = line.RecipeId,
                    ItemId = line.Recipe.OutputItemId,
                    ItemName = line.Recipe.OutputItem.Name,
                    Quantity = line.Quantity,
                })
                .ToList(),
            OnLists = item.OnLists
                .OrderBy(entry => entry.List.Name)
                .Select(entry => new ListUse
                {
                    ListId = entry.ListId,
                    ListName = entry.List.Name,
                    Quantity = entry.Quantity,
                })
                .ToList(),
        };
}
