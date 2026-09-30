using System.Net;
using System.Net.Http.Json;
using CraftingPlanner.Api.Contracts;
using static CraftingPlanner.Tests.PlannerApp;

namespace CraftingPlanner.Tests;

/// <summary>
/// Reads items on the running app. These tests change nothing, so they share one app and
/// one database and can rely on the sample data being exactly as seeded.
/// </summary>
/// <param name="app">The app, started once for the class.</param>
public class ItemReadTests(PlannerApp app) : IClassFixture<PlannerApp>
{
    private readonly HttpClient client = app.CreateClient();

    /// <summary>The sample data lists 19 items, sorted by name, 20 to a page.</summary>
    [Fact]
    public async Task List_ReturnsTheSampleItemsSortedByName()
    {
        var page = await client.GetFromJsonAsync<PagedResult<ItemSummary>>("/api/items", Json);

        Assert.NotNull(page);
        Assert.Equal(19, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal("Arrow", page.Items[0].Name);
        Assert.Equal(page.Items.Select(item => item.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase), page.Items.Select(item => item.Name));
    }

    /// <summary>Filters combine: crafted items with "iron" in the name or description.</summary>
    [Fact]
    public async Task List_CombinesFilters()
    {
        var page = await client.GetFromJsonAsync<PagedResult<ItemSummary>>("/api/items?kind=crafted&search=IRON", Json);

        Assert.NotNull(page);
        Assert.Equal(["Iron Ingot", "Sword"], page.Items.Select(item => item.Name));
        Assert.All(page.Items, item => Assert.Equal(ItemKind.Crafted, item.Kind));
    }

    /// <summary>A bad filter value is a 400 that names the field.</summary>
    [Fact]
    public async Task List_RefusesABadFilter()
    {
        var response = await client.GetAsync("/api/items?kind=banana&pageSize=500");
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(body["errors"]!["kind"]);
        Assert.NotNull(body["errors"]!["pageSize"]);
    }

    /// <summary>One item comes with its recipe and everything it is used in.</summary>
    [Fact]
    public async Task Get_IncludesTheRecipeAndUses()
    {
        var id = await IdOfAsync(client, "Stick");

        var stick = await client.GetFromJsonAsync<ItemDetail>($"/api/items/{id}", Json);

        Assert.NotNull(stick);
        Assert.Equal(ItemKind.Crafted, stick.Kind);
        Assert.NotNull(stick.Recipe);
        Assert.Equal(4, stick.Recipe.OutputQuantity);
        Assert.Equal(["Plank"], stick.Recipe.Ingredients.Select(line => line.ItemName));
        Assert.Equal(["Arrow", "Bow", "Pickaxe", "Sword", "Torch"], stick.UsedIn.Select(use => use.ItemName));
    }

    /// <summary>An unknown id is a 404 in the standard format; a non-numeric id is a 400.</summary>
    [Fact]
    public async Task Get_RefusesUnknownAndMalformedIds()
    {
        var unknown = await client.GetAsync("/api/items/9999");
        var malformed = await client.GetAsync("/api/items/abc");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("The item was not found", (string?)(await BodyAsync(unknown))["title"]);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    /// <summary>Deleting an item that recipes use is refused, and the rejection lists them.</summary>
    [Fact]
    public async Task Delete_RefusesAnItemInUse()
    {
        var id = await IdOfAsync(client, "Stick");

        var response = await client.DeleteAsync($"/api/items/{id}");
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The item is in use", (string?)body["title"]);
        Assert.Equal(5, body["usedIn"]!.AsArray().Count);
    }
}

/// <summary>
/// Creates, changes, and deletes items on the running app. Every test in this class shares
/// one app and one database, so each uses item names of its own.
/// </summary>
/// <param name="app">The app, started once for the class.</param>
public class ItemChangeTests(PlannerApp app) : IClassFixture<PlannerApp>
{
    private readonly HttpClient client = app.CreateClient();

    /// <summary>Create returns 201 with the new item's address, and the item is then raw.</summary>
    [Fact]
    public async Task Create_ReturnsTheNewItemAndItsAddress()
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name = "  Copper Ore  ", category = "Raw Material" });
        var created = await response.Content.ReadFromJsonAsync<ItemDetail>(Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Copper Ore", created.Name);
        Assert.Equal(ItemKind.Raw, created.Kind);
        Assert.Equal($"/api/items/{created.Id}", response.Headers.Location?.PathAndQuery);

        var fetched = await client.GetFromJsonAsync<ItemDetail>(response.Headers.Location!, Json);
        Assert.Equal("Copper Ore", fetched?.Name);
    }

    /// <summary>A missing name is a 400 that names the field with a plain message.</summary>
    [Fact]
    public async Task Create_RefusesAMissingName()
    {
        var response = await client.PostAsJsonAsync("/api/items", new { description = "no name" });
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("The name is required.", body["errors"]!["name"]!.AsArray().Select(node => (string)node!));
    }

    /// <summary>A name that is already used, in any letter case, is a 409.</summary>
    [Fact]
    public async Task Create_RefusesADuplicateName()
    {
        var response = await client.PostAsJsonAsync("/api/items", new { name = "STICK" });
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The name is already in use", (string?)body["title"]);
    }

    /// <summary>PATCH changes only the fields that are sent, and an empty string clears a field.</summary>
    [Fact]
    public async Task Patch_ChangesOnlyWhatIsSent()
    {
        var created = await (await client.PostAsJsonAsync("/api/items", new { name = "Tin Ore", description = "Grey rock.", category = "Raw Material" }))
            .Content.ReadFromJsonAsync<ItemDetail>(Json);

        var response = await client.PatchAsJsonAsync($"/api/items/{created!.Id}", new { category = "Ore", description = "" });
        var changed = await response.Content.ReadFromJsonAsync<ItemDetail>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Tin Ore", changed?.Name);
        Assert.Equal("Ore", changed?.Category);
        Assert.Null(changed?.Description);
    }

    /// <summary>PUT replaces every field, so a field left out is cleared.</summary>
    [Fact]
    public async Task Put_ReplacesEveryField()
    {
        var created = await (await client.PostAsJsonAsync("/api/items", new { name = "Silver Ore", description = "Shiny.", category = "Raw Material" }))
            .Content.ReadFromJsonAsync<ItemDetail>(Json);

        var response = await client.PutAsJsonAsync($"/api/items/{created!.Id}", new { name = "Silver Ore" });
        var replaced = await response.Content.ReadFromJsonAsync<ItemDetail>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(replaced?.Description);
        Assert.Null(replaced?.Category);
    }

    /// <summary>Deleting an unused item returns 204, and the item is then gone.</summary>
    [Fact]
    public async Task Delete_RemovesAnUnusedItem()
    {
        var created = await (await client.PostAsJsonAsync("/api/items", new { name = "Pebble" }))
            .Content.ReadFromJsonAsync<ItemDetail>(Json);

        var deleted = await client.DeleteAsync($"/api/items/{created!.Id}");
        var afterwards = await client.GetAsync($"/api/items/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }
}
