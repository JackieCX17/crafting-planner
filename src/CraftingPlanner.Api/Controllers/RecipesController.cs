using System.ComponentModel.DataAnnotations;
using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Data;
using CraftingPlanner.Api.Models;
using CraftingPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CraftingPlanner.Api.Controllers;

/// <summary>
/// Endpoints for recipes and the ingredients inside them. A recipe says what goes into
/// an item and how many come out.
/// </summary>
/// <param name="db">The database. Supplied automatically for each request.</param>
[Route("api/recipes")]
[Tags("Recipes")]
public class RecipesController(PlannerDbContext db) : PlannerControllerBase
{
    /// <summary>Lists recipes.</summary>
    /// <remarks>
    /// Recipes are sorted by the name of the item they make. Set <c>uses</c> to an item's id
    /// to see only the recipes that have that item as an ingredient.
    /// </remarks>
    /// <param name="uses">Return only recipes that use the item with this id as an ingredient.</param>
    /// <param name="page">The page to return. The first page is 1.</param>
    /// <param name="pageSize">How many recipes a page holds, from 1 to 100.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>One page of matching recipes, with the total number that matched.</returns>
    /// <response code="200">The matching recipes. The list is empty when nothing matched.</response>
    /// <response code="400">A filter or paging value is not valid.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<RecipeDetail>>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public async Task<ActionResult<PagedResult<RecipeDetail>>> List(
        [FromQuery, Range(1, int.MaxValue)] int? uses = null,
        [FromQuery, Range(1, 100_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Recipe> recipes = db.Recipes.AsNoTracking();

        if (uses is { } itemId)
        {
            recipes = recipes.Where(r => r.Ingredients.Any(line => line.ItemId == itemId));
        }

        var totalCount = await recipes.CountAsync(cancellationToken);

        var pageOfRecipes = await recipes
            .OrderBy(r => r.OutputItem.Name)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(r => r.OutputItem)
            .Include(r => r.Ingredients)
                .ThenInclude(line => line.Item)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedResult<RecipeDetail>
        {
            Items = pageOfRecipes.Select(ToDetail).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <summary>Gets one recipe.</summary>
    /// <param name="id">Id of the recipe.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The recipe with its ingredients.</returns>
    /// <response code="200">The recipe.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">There is no recipe with that id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType<RecipeDetail>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<RecipeDetail>> Get(int id, CancellationToken cancellationToken)
    {
        var recipe = await FindWithLinksAsync(id, cancellationToken);

        return recipe is null
            ? NotFoundRejection("recipe", id)
            : ToDetail(recipe);
    }

    /// <summary>Creates a recipe together with its ingredients.</summary>
    /// <remarks>
    /// The item the recipe makes changes from raw to crafted. An item has one recipe, so
    /// creating a second recipe for the same item is refused. A recipe that would loop,
    /// such as a Plank that needs a Stick while a Stick needs a Plank, is refused too, and
    /// the rejection shows the loop in its <c>loop</c> field.
    /// </remarks>
    /// <param name="request">The new recipe.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The recipe as saved, including its new id.</returns>
    /// <response code="201">The recipe was created. The Location header holds its address.</response>
    /// <response code="400">A field is missing or not valid, an item is listed twice, or an item does not exist.</response>
    /// <response code="409">The item already has a recipe, or the recipe would loop.</response>
    [HttpPost]
    [ProducesResponseType<RecipeDetail>(StatusCodes.Status201Created, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<RecipeDetail>> Create(
        RecipeCreateRequest request,
        CancellationToken cancellationToken)
    {
        var output = await db.Items
            .AsNoTracking()
            .Include(i => i.Recipe)
            .FirstOrDefaultAsync(i => i.Id == request.OutputItemId, cancellationToken);

        if (output is null)
        {
            ModelState.AddModelError("outputItemId", $"There is no item with id {request.OutputItemId}.");
            return ValidationProblem(ModelState);
        }

        if (output.Recipe is not null)
        {
            return Rejection(
                StatusCodes.Status409Conflict,
                "The item already has a recipe",
                $"\"{output.Name}\" is already made by recipe {output.Recipe.Id}. An item has one recipe, "
                + "so replace that recipe instead of creating another.",
                extraName: "recipeId",
                extraValue: output.Recipe.Id);
        }

        var problem = await FindIngredientProblemAsync(output.Id, request.Ingredients, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        var recipe = new Recipe
        {
            OutputItemId = output.Id,
            OutputQuantity = request.OutputQuantity,
            CraftSeconds = request.CraftSeconds,
            Ingredients = request.Ingredients
                .Select(ingredient => new RecipeIngredient
                {
                    ItemId = ingredient.ItemId,
                    Quantity = ingredient.Quantity,
                })
                .ToList(),
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(cancellationToken);

        var saved = await FindWithLinksAsync(recipe.Id, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = recipe.Id }, ToDetail(saved!));
    }

    /// <summary>Replaces a recipe: how many it makes, how long it takes, and all of its ingredients.</summary>
    /// <remarks>
    /// The list of ingredients that is sent becomes the whole list. Ingredients that are left
    /// out are removed. The item the recipe makes cannot be changed.
    /// </remarks>
    /// <param name="id">Id of the recipe.</param>
    /// <param name="request">The recipe's new contents.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The recipe as saved.</returns>
    /// <response code="200">The recipe was replaced.</response>
    /// <response code="400">A field is missing or not valid, an item is listed twice, or an item does not exist.</response>
    /// <response code="404">There is no recipe with that id.</response>
    /// <response code="409">The recipe would loop.</response>
    [HttpPut("{id}")]
    [ProducesResponseType<RecipeDetail>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<RecipeDetail>> Replace(
        int id,
        RecipeReplaceRequest request,
        CancellationToken cancellationToken)
    {
        var recipe = await FindForChangeAsync(id, cancellationToken);
        if (recipe is null)
        {
            return NotFoundRejection("recipe", id);
        }

        var problem = await FindIngredientProblemAsync(recipe.OutputItemId, request.Ingredients, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        recipe.OutputQuantity = request.OutputQuantity;
        recipe.CraftSeconds = request.CraftSeconds;

        // Lines are changed in place, not deleted and added again, so an ingredient that
        // stays in the recipe keeps its row in the database.
        var wanted = request.Ingredients.ToDictionary(i => i.ItemId, i => i.Quantity);
        recipe.Ingredients.RemoveAll(line => !wanted.ContainsKey(line.ItemId));

        foreach (var (itemId, quantity) in wanted)
        {
            var line = recipe.Ingredients.FirstOrDefault(l => l.ItemId == itemId);
            if (line is null)
            {
                recipe.Ingredients.Add(new RecipeIngredient { ItemId = itemId, Quantity = quantity });
            }
            else
            {
                line.Quantity = quantity;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var saved = await FindWithLinksAsync(id, cancellationToken);

        return ToDetail(saved!);
    }

    /// <summary>Deletes a recipe.</summary>
    /// <remarks>
    /// The item the recipe made is kept and becomes raw. Recipes that use that item as an
    /// ingredient are not affected.
    /// </remarks>
    /// <param name="id">Id of the recipe.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Nothing.</returns>
    /// <response code="204">The recipe was deleted.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">There is no recipe with that id.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (recipe is null)
        {
            return NotFoundRejection("recipe", id);
        }

        db.Recipes.Remove(recipe);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Lists the ingredients of a recipe.</summary>
    /// <param name="id">Id of the recipe.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The ingredients, in name order.</returns>
    /// <response code="200">The ingredients.</response>
    /// <response code="400">The id is not a whole number.</response>
    /// <response code="404">There is no recipe with that id.</response>
    [HttpGet("{id}/ingredients")]
    [Tags("Ingredients")]
    [ProducesResponseType<IReadOnlyList<IngredientLine>>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<IReadOnlyList<IngredientLine>>> ListIngredients(
        int id,
        CancellationToken cancellationToken)
    {
        var recipe = await FindWithLinksAsync(id, cancellationToken);
        if (recipe is null)
        {
            return NotFoundRejection("recipe", id);
        }

        return Ok(ToDetail(recipe).Ingredients);
    }

    /// <summary>Adds one ingredient to a recipe, or changes the quantity of one it already has.</summary>
    /// <remarks>
    /// The other ingredients are not affected. Sending the same request twice gives the same
    /// result as sending it once. An ingredient that would make the recipe loop is refused.
    /// </remarks>
    /// <param name="id">Id of the recipe.</param>
    /// <param name="itemId">Id of the item to use as an ingredient.</param>
    /// <param name="request">The quantity one craft uses.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The ingredient as saved.</returns>
    /// <response code="200">The quantity of an existing ingredient was changed.</response>
    /// <response code="201">The ingredient was added.</response>
    /// <response code="400">The quantity is not valid, or an id is not a whole number.</response>
    /// <response code="404">There is no recipe with that id, or no item with that id.</response>
    /// <response code="409">The ingredient would make the recipe loop, or the recipe already has 20 ingredients.</response>
    [HttpPut("{id}/ingredients/{itemId}")]
    [Tags("Ingredients")]
    [ProducesResponseType<IngredientLine>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<IngredientLine>(StatusCodes.Status201Created, Json)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<ActionResult<IngredientLine>> SetIngredient(
        int id,
        int itemId,
        IngredientQuantityRequest request,
        CancellationToken cancellationToken)
    {
        var recipe = await FindForChangeAsync(id, cancellationToken);
        if (recipe is null)
        {
            return NotFoundRejection("recipe", id);
        }

        var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item is null)
        {
            return NotFoundRejection("item", itemId);
        }

        var saved = new IngredientLine { ItemId = item.Id, ItemName = item.Name, Quantity = request.Quantity };

        var existing = recipe.Ingredients.FirstOrDefault(line => line.ItemId == itemId);
        if (existing is not null)
        {
            existing.Quantity = request.Quantity;
            await db.SaveChangesAsync(cancellationToken);

            return saved;
        }

        if (recipe.Ingredients.Count >= RecipeRules.MostIngredients)
        {
            return Rejection(
                StatusCodes.Status409Conflict,
                "The recipe is full",
                $"Recipe {id} already has {RecipeRules.MostIngredients} ingredients, which is the most a recipe can have.");
        }

        var loop = await FindLoopAsync(recipe.OutputItemId, [itemId], cancellationToken);
        if (loop is not null)
        {
            return await LoopRejectionAsync(loop, cancellationToken);
        }

        recipe.Ingredients.Add(new RecipeIngredient { ItemId = itemId, Quantity = request.Quantity });
        await db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(ListIngredients), new { id }, saved);
    }

    /// <summary>Removes one ingredient from a recipe.</summary>
    /// <remarks>
    /// A recipe needs at least one ingredient, so its last ingredient cannot be removed.
    /// Delete the recipe instead.
    /// </remarks>
    /// <param name="id">Id of the recipe.</param>
    /// <param name="itemId">Id of the ingredient's item.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>Nothing.</returns>
    /// <response code="204">The ingredient was removed.</response>
    /// <response code="400">An id is not a whole number.</response>
    /// <response code="404">There is no recipe with that id, or the recipe does not use that item.</response>
    /// <response code="409">It is the recipe's only ingredient.</response>
    [HttpDelete("{id}/ingredients/{itemId}")]
    [Tags("Ingredients")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemJson)]
    public async Task<IActionResult> RemoveIngredient(int id, int itemId, CancellationToken cancellationToken)
    {
        var recipe = await FindForChangeAsync(id, cancellationToken);
        if (recipe is null)
        {
            return NotFoundRejection("recipe", id);
        }

        var line = recipe.Ingredients.FirstOrDefault(l => l.ItemId == itemId);
        if (line is null)
        {
            return Rejection(
                StatusCodes.Status404NotFound,
                "The ingredient was not found",
                $"Recipe {id} does not use item {itemId}.");
        }

        if (recipe.Ingredients.Count == 1)
        {
            return Rejection(
                StatusCodes.Status409Conflict,
                "The recipe needs an ingredient",
                $"Item {itemId} is the only ingredient of recipe {id}, and a recipe needs at least one. "
                + "To remove it, delete the recipe.");
        }

        recipe.Ingredients.Remove(line);
        await db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Checks a list of ingredients against the rules that need the database: each item
    /// listed once, each item exists, and no loop.
    /// </summary>
    /// <param name="outputItemId">Id of the item the recipe makes.</param>
    /// <param name="ingredients">The ingredients that were sent.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The rejection to send back, or null when the ingredients are acceptable.</returns>
    private async Task<ActionResult?> FindIngredientProblemAsync(
        int outputItemId,
        List<IngredientRequest> ingredients,
        CancellationToken cancellationToken)
    {
        var itemIds = ingredients.Select(i => i.ItemId).Distinct().ToList();

        var existingIds = await db.Items
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        var seen = new HashSet<int>();
        for (var position = 0; position < ingredients.Count; position++)
        {
            var itemId = ingredients[position].ItemId;
            var field = $"ingredients[{position}].itemId";

            if (!seen.Add(itemId))
            {
                ModelState.AddModelError(
                    field,
                    $"Item {itemId} is listed more than once. List each item once, with its total quantity.");
            }
            else if (!existingIds.Contains(itemId))
            {
                ModelState.AddModelError(field, $"There is no item with id {itemId}.");
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var loop = await FindLoopAsync(outputItemId, itemIds, cancellationToken);

        return loop is null
            ? null
            : await LoopRejectionAsync(loop, cancellationToken);
    }

    /// <summary>Checks whether giving an item these ingredients would create a loop.</summary>
    /// <remarks>
    /// Every recipe link is loaded into memory and searched there. That is one small query,
    /// and it suits a data set of this size. A much larger one would do the search inside
    /// the database.
    /// </remarks>
    /// <param name="outputItemId">Id of the item the recipe makes.</param>
    /// <param name="ingredientIds">Ids of the items the recipe would use.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The loop as a chain of item ids, or null when there is none.</returns>
    private async Task<IReadOnlyList<int>?> FindLoopAsync(
        int outputItemId,
        IReadOnlyList<int> ingredientIds,
        CancellationToken cancellationToken)
    {
        // The recipe being checked is left out: its old ingredients are about to be replaced.
        var links = await db.RecipeIngredients
            .Where(line => line.Recipe.OutputItemId != outputItemId)
            .Select(line => new { line.Recipe.OutputItemId, line.ItemId })
            .ToListAsync(cancellationToken);

        var ingredientsByItem = links
            .GroupBy(link => link.OutputItemId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<int>)group.Select(link => link.ItemId).ToList());

        return RecipeLoopFinder.FindLoop(outputItemId, ingredientIds, ingredientsByItem);
    }

    /// <summary>Builds the 409 rejection for a recipe that would loop, naming each item in the loop.</summary>
    /// <param name="loop">The loop as a chain of item ids.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The response to send back.</returns>
    private async Task<ObjectResult> LoopRejectionAsync(IReadOnlyList<int> loop, CancellationToken cancellationToken)
    {
        var ids = loop.Distinct().ToList();
        var names = await db.Items
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, cancellationToken);

        var chain = loop.Select(id => names[id]).ToList();

        // Each item paired with the one after it: "Plank" needs "Stick", "Stick" needs "Plank".
        var steps = chain
            .Zip(chain.Skip(1), (item, needs) => $"\"{item}\" needs \"{needs}\"")
            .ToList();

        // A chain of two is an item listed as its own ingredient.
        var detail = chain.Count == 2
            ? $"\"{chain[0]}\" was listed as its own ingredient. A recipe cannot use the item it makes."
            : "These ingredients would create a loop: "
                + string.Join(", ", steps.SkipLast(1))
                + $", and {steps[^1]}.";

        return Rejection(
            StatusCodes.Status409Conflict,
            "The recipe would loop",
            detail,
            extraName: "loop",
            extraValue: chain);
    }

    /// <summary>Loads a recipe and its ingredient lines so they can be changed and saved.</summary>
    /// <param name="id">Id of the recipe.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The recipe, or null when there is none with that id.</returns>
    private Task<Recipe?> FindForChangeAsync(int id, CancellationToken cancellationToken) =>
        db.Recipes
            .Include(r => r.Ingredients)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <summary>Loads a recipe for reading, together with the names of the items it involves.</summary>
    /// <param name="id">Id of the recipe.</param>
    /// <param name="cancellationToken">Signals that the caller has stopped waiting.</param>
    /// <returns>The recipe, or null when there is none with that id.</returns>
    private Task<Recipe?> FindWithLinksAsync(int id, CancellationToken cancellationToken) =>
        db.Recipes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.OutputItem)
            .Include(r => r.Ingredients)
                .ThenInclude(line => line.Item)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <summary>Converts a database row into the shape the API returns.</summary>
    /// <param name="recipe">The recipe, loaded with its output item and ingredients.</param>
    /// <returns>The recipe as the API returns it.</returns>
    private static RecipeDetail ToDetail(Recipe recipe) =>
        new()
        {
            Id = recipe.Id,
            OutputItemId = recipe.OutputItemId,
            OutputItemName = recipe.OutputItem.Name,
            OutputQuantity = recipe.OutputQuantity,
            CraftSeconds = recipe.CraftSeconds,
            Ingredients = recipe.Ingredients
                .OrderBy(line => line.Item.Name)
                .Select(line => new IngredientLine
                {
                    ItemId = line.ItemId,
                    ItemName = line.Item.Name,
                    Quantity = line.Quantity,
                })
                .ToList(),
        };
}
