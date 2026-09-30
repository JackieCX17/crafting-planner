using System.Net;
using System.Net.Http.Json;
using CraftingPlanner.Api.Contracts;
using static CraftingPlanner.Tests.PlannerApp;

namespace CraftingPlanner.Tests;

/// <summary>
/// Calls the recipe and ingredient endpoints on the running app. Every test in this class
/// shares one app and one database, so tests that change data create items of their own.
/// </summary>
/// <param name="app">The app, started once for the class.</param>
public class RecipeEndpointTests(PlannerApp app) : IClassFixture<PlannerApp>
{
    private readonly HttpClient client = app.CreateClient();

    /// <summary>Creating a recipe turns its item from raw to crafted, with nothing written to the item.</summary>
    [Fact]
    public async Task Create_TurnsTheItemCrafted()
    {
        var steel = await NewItemAsync("Steel Ingot");
        var ironIngot = await IdOfAsync(client, "Iron Ingot");
        var coal = await IdOfAsync(client, "Coal");

        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            outputItemId = steel,
            outputQuantity = 2,
            craftSeconds = 15,
            ingredients = new[] { new { itemId = ironIngot, quantity = 3 }, new { itemId = coal, quantity = 1 } },
        });
        var recipe = await response.Content.ReadFromJsonAsync<RecipeDetail>(Json);
        var item = await client.GetFromJsonAsync<ItemDetail>($"/api/items/{steel}", Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/recipes/{recipe!.Id}", response.Headers.Location?.PathAndQuery);
        Assert.Equal(["Coal", "Iron Ingot"], recipe.Ingredients.Select(line => line.ItemName));
        Assert.Equal(ItemKind.Crafted, item!.Kind);
        Assert.Equal(recipe.Id, item.Recipe?.Id);
    }

    /// <summary>A second recipe for an item is refused, naming the one that exists.</summary>
    [Fact]
    public async Task Create_RefusesASecondRecipe()
    {
        var stick = await IdOfAsync(client, "Stick");
        var log = await IdOfAsync(client, "Log");

        var response = await client.PostAsJsonAsync("/api/recipes", new { outputItemId = stick, ingredients = new[] { new { itemId = log, quantity = 1 } } });
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The item already has a recipe", (string?)body["title"]);
        Assert.NotNull(body["recipeId"]);
    }

    /// <summary>Problems inside the ingredient list are reported against their positions, all at once.</summary>
    [Fact]
    public async Task Create_ReportsEveryIngredientProblemAtItsPosition()
    {
        var target = await NewItemAsync("Bronze Ingot");
        var coal = await IdOfAsync(client, "Coal");

        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            outputItemId = target,
            ingredients = new[] { new { itemId = coal, quantity = 1 }, new { itemId = coal, quantity = 2 }, new { itemId = 777, quantity = 1 } },
        });
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body["errors"]!["ingredients[1].itemId"]);
        Assert.NotNull(body["errors"]!["ingredients[2].itemId"]);
    }

    /// <summary>Making Planks from Sticks would loop, because Sticks are made from Planks. The rejection shows the loop.</summary>
    [Fact]
    public async Task Replace_RefusesALoopAndShowsIt()
    {
        var plank = await client.GetFromJsonAsync<ItemDetail>($"/api/items/{await IdOfAsync(client, "Plank")}", Json);
        var stick = await IdOfAsync(client, "Stick");

        var response = await client.PutAsJsonAsync($"/api/recipes/{plank!.Recipe!.Id}", new { outputQuantity = 2, ingredients = new[] { new { itemId = stick, quantity = 1 } } });
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The recipe would loop", (string?)body["title"]);
        Assert.Equal(["Plank", "Stick", "Plank"], body["loop"]!.AsArray().Select(node => (string)node!));
    }

    /// <summary>A loop several recipes deep is found too.</summary>
    [Fact]
    public async Task Create_FindsADeepLoop()
    {
        var log = await IdOfAsync(client, "Log");
        var toolKit = await IdOfAsync(client, "Tool Kit");

        var response = await client.PostAsJsonAsync("/api/recipes", new { outputItemId = log, ingredients = new[] { new { itemId = toolKit, quantity = 1 } } });
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(["Log", "Tool Kit", "Sword", "Stick", "Plank", "Log"], body["loop"]!.AsArray().Select(node => (string)node!));
    }

    /// <summary>One address adds an ingredient (201) or changes its quantity (200), and removes it (204).</summary>
    [Fact]
    public async Task Ingredient_IsAddedChangedAndRemovedAtOneAddress()
    {
        var sword = await client.GetFromJsonAsync<ItemDetail>($"/api/items/{await IdOfAsync(client, "Sword")}", Json);
        var coal = await IdOfAsync(client, "Coal");
        var address = $"/api/recipes/{sword!.Recipe!.Id}/ingredients/{coal}";

        var added = await client.PutAsJsonAsync(address, new { quantity = 1 });
        var changed = await client.PutAsJsonAsync(address, new { quantity = 2 });
        var line = await changed.Content.ReadFromJsonAsync<IngredientLine>(Json);
        var removed = await client.DeleteAsync(address);
        var again = await client.DeleteAsync(address);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(("Coal", 2), (line!.ItemName, line.Quantity));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    /// <summary>The last ingredient of a recipe cannot be removed.</summary>
    [Fact]
    public async Task Ingredient_TheLastOneCannotBeRemoved()
    {
        var plank = await client.GetFromJsonAsync<ItemDetail>($"/api/items/{await IdOfAsync(client, "Plank")}", Json);
        var log = await IdOfAsync(client, "Log");

        var response = await client.DeleteAsync($"/api/recipes/{plank!.Recipe!.Id}/ingredients/{log}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The recipe needs an ingredient", (string?)(await BodyAsync(response))["title"]);
    }

    /// <summary>Deleting a recipe keeps its item, which becomes raw.</summary>
    [Fact]
    public async Task Delete_KeepsTheItemAsRaw()
    {
        var target = await NewItemAsync("Glass");
        var coal = await IdOfAsync(client, "Coal");
        var recipe = await (await client.PostAsJsonAsync("/api/recipes", new { outputItemId = target, ingredients = new[] { new { itemId = coal, quantity = 1 } } }))
            .Content.ReadFromJsonAsync<RecipeDetail>(Json);

        var deleted = await client.DeleteAsync($"/api/recipes/{recipe!.Id}");
        var item = await client.GetFromJsonAsync<ItemDetail>($"/api/items/{target}", Json);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(ItemKind.Raw, item!.Kind);
        Assert.Null(item.Recipe);
    }

    /// <summary>The list can be narrowed to the recipes that use one item.</summary>
    [Fact]
    public async Task List_FiltersByIngredient()
    {
        var stick = await IdOfAsync(client, "Stick");

        var page = await client.GetFromJsonAsync<PagedResult<RecipeDetail>>($"/api/recipes?uses={stick}&pageSize=50", Json);

        Assert.NotNull(page);
        Assert.Equal(["Arrow", "Bow", "Pickaxe", "Sword", "Torch"], page.Items.Select(recipe => recipe.OutputItemName));
    }

    /// <summary>Creates a raw item for a test to give a recipe to.</summary>
    /// <param name="name">A name no other test uses.</param>
    /// <returns>The new item's id.</returns>
    private async Task<int> NewItemAsync(string name)
    {
        var created = await (await client.PostAsJsonAsync("/api/items", new { name })).Content.ReadFromJsonAsync<ItemDetail>(Json);
        return created!.Id;
    }
}
