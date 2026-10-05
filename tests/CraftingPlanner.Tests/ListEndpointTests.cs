using System.Net;
using System.Net.Http.Json;
using CraftingPlanner.Api.Contracts;
using static CraftingPlanner.Tests.PlannerApp;

namespace CraftingPlanner.Tests;

/// <summary>
/// Calls the shopping list endpoints on the running app. Every test in this class shares
/// one app and one database, so each uses list names of its own.
/// </summary>
/// <param name="app">The app, started once for the class.</param>
public class ListEndpointTests(PlannerApp app) : IClassFixture<PlannerApp>
{
    private readonly HttpClient client = app.CreateClient();

    /// <summary>A new list is empty, has an address, and appears in the list of lists.</summary>
    [Fact]
    public async Task Create_MakesAnEmptyList()
    {
        var response = await client.PostAsJsonAsync("/api/lists", new { name = "  Day one  ", description = "First things to make." });
        var created = await response.Content.ReadFromJsonAsync<ListDetail>(Json);
        var page = await client.GetFromJsonAsync<PagedResult<ListSummary>>("/api/lists?pageSize=100", Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/lists/{created!.Id}", response.Headers.Location?.PathAndQuery);
        Assert.Equal(("Day one", 0), (created.Name, created.EntryCount));
        Assert.Empty(created.Entries);
        Assert.Contains(page!.Items, list => list.Id == created.Id);
    }

    /// <summary>A name already used by another list, in any letter case, is a 409.</summary>
    [Fact]
    public async Task Create_RefusesADuplicateName()
    {
        await client.PostAsJsonAsync("/api/lists", new { name = "Weekend build" });

        var response = await client.PostAsJsonAsync("/api/lists", new { name = "WEEKEND BUILD" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The name is already in use", (string?)(await BodyAsync(response))["title"]);
    }

    /// <summary>Putting an item on a list returns 201; sending it again with a new quantity returns 200.</summary>
    [Fact]
    public async Task SetEntry_AddsThenChanges()
    {
        var list = await NewListAsync("Entries");
        var sword = await IdOfAsync(client, "Sword");
        var address = $"/api/lists/{list}/items/{sword}";

        var added = await client.PutAsJsonAsync(address, new { quantity = 2 });
        var changed = await client.PutAsJsonAsync(address, new { quantity = 5 });
        var entry = await changed.Content.ReadFromJsonAsync<ListEntryLine>(Json);
        var detail = await client.GetFromJsonAsync<ListDetail>($"/api/lists/{list}", Json);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(("Sword", 5L, ItemKind.Crafted, "broadsword"), (entry!.ItemName, entry.Quantity, entry.Kind, entry.Icon));
        Assert.Equal(1, detail!.EntryCount);
    }

    /// <summary>An item that does not exist cannot be put on a list, and a quantity out of range is refused.</summary>
    [Fact]
    public async Task SetEntry_RefusesBadInput()
    {
        var list = await NewListAsync("Bad entries");
        var sword = await IdOfAsync(client, "Sword");

        var unknownItem = await client.PutAsJsonAsync($"/api/lists/{list}/items/9999", new { quantity = 1 });
        var badQuantity = await client.PutAsJsonAsync($"/api/lists/{list}/items/{sword}", new { quantity = 0 });
        var unknownList = await client.PutAsJsonAsync($"/api/lists/9999/items/{sword}", new { quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, unknownItem.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badQuantity.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownList.StatusCode);
    }

    /// <summary>Taking an item off a list returns 204, and taking it off again is a 404.</summary>
    [Fact]
    public async Task RemoveEntry_TakesTheItemOff()
    {
        var list = await NewListAsync("Removal");
        var coal = await IdOfAsync(client, "Coal");
        await client.PutAsJsonAsync($"/api/lists/{list}/items/{coal}", new { quantity = 3 });

        var removed = await client.DeleteAsync($"/api/lists/{list}/items/{coal}");
        var again = await client.DeleteAsync($"/api/lists/{list}/items/{coal}");

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    /// <summary>The plan for a list totals its entries together: 2 Swords and 1 Pickaxe share ingots and sticks.</summary>
    [Fact]
    public async Task Plan_TotalsTheWholeList()
    {
        var list = await NewListAsync("Sword and pickaxe");
        await client.PutAsJsonAsync($"/api/lists/{list}/items/{await IdOfAsync(client, "Sword")}", new { quantity = 2 });
        await client.PutAsJsonAsync($"/api/lists/{list}/items/{await IdOfAsync(client, "Pickaxe")}", new { quantity = 1 });

        var plan = await client.GetFromJsonAsync<ListPlanResponse>($"/api/lists/{list}/plan", Json);

        Assert.NotNull(plan);
        Assert.Equal(["Pickaxe", "Sword"], plan.Targets.Select(target => target.ItemName));
        Assert.Equal([("Coal", 7L), ("Iron Ore", 14L), ("Log", 1L)], plan.RawMaterials.Select(raw => (raw.ItemName, raw.Quantity)));
        var stick = plan.Steps.Single(step => step.ItemName == "Stick");
        Assert.Equal((4L, 1L, 4L, 0L), (stick.Needed, stick.Crafts, stick.Made, stick.Leftover));
        Assert.Equal(98, plan.TotalSeconds);
    }

    /// <summary>An empty list plans to nothing, rather than being refused.</summary>
    [Fact]
    public async Task Plan_OfAnEmptyList_IsEmpty()
    {
        var list = await NewListAsync("Nothing yet");

        var plan = await client.GetFromJsonAsync<ListPlanResponse>($"/api/lists/{list}/plan", Json);

        Assert.NotNull(plan);
        Assert.Empty(plan.RawMaterials);
        Assert.Empty(plan.Steps);
    }

    /// <summary>The export is a CSV download named after the list.</summary>
    [Fact]
    public async Task Export_IsNamedAfterTheList()
    {
        var list = await NewListAsync("Hunting trip");
        await client.PutAsJsonAsync($"/api/lists/{list}/items/{await IdOfAsync(client, "Bow")}", new { quantity = 1 });

        var response = await client.GetAsync($"/api/lists/{list}/plan/export");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("list-hunting-trip.csv", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Contains("Step,Bow,1,1,1,0,6", text);
    }

    /// <summary>Deleting an item removes it from every list it is on, and the item page says which lists those are.</summary>
    [Fact]
    public async Task DeletingAnItem_RemovesItFromLists()
    {
        var list = await NewListAsync("Soon to shrink");
        var created = await ReadAsync<ItemDetail>(await client.PostAsJsonAsync("/api/items", new { name = "Temporary Gadget" }));
        await client.PutAsJsonAsync($"/api/lists/{list}/items/{created!.Id}", new { quantity = 4 });

        var itemBefore = await client.GetFromJsonAsync<ItemDetail>($"/api/items/{created.Id}", Json);
        var deleted = await client.DeleteAsync($"/api/items/{created.Id}");
        var listAfter = await client.GetFromJsonAsync<ListDetail>($"/api/lists/{list}", Json);

        Assert.Equal(("Soon to shrink", 4L), (itemBefore!.OnLists.Single().ListName, itemBefore.OnLists.Single().Quantity));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(listAfter!.Entries);
    }

    /// <summary>Renaming a list keeps its entries; deleting it does not touch the items.</summary>
    [Fact]
    public async Task RenameAndDelete_LeaveItemsAlone()
    {
        var list = await NewListAsync("Old name");
        var torch = await IdOfAsync(client, "Torch");
        await client.PutAsJsonAsync($"/api/lists/{list}/items/{torch}", new { quantity = 16 });

        var renamed = await client.PutAsJsonAsync($"/api/lists/{list}", new { name = "New name" });
        var detail = await renamed.Content.ReadFromJsonAsync<ListDetail>(Json);
        var deleted = await client.DeleteAsync($"/api/lists/{list}");
        var gone = await client.GetAsync($"/api/lists/{list}");
        var torchStill = await client.GetAsync($"/api/items/{torch}");

        Assert.Equal(("New name", 1), (detail!.Name, detail.EntryCount));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(HttpStatusCode.OK, torchStill.StatusCode);
    }

    /// <summary>Creates an empty list for a test.</summary>
    /// <param name="name">A name no other test uses.</param>
    /// <returns>The new list's id.</returns>
    private async Task<int> NewListAsync(string name)
    {
        var created = await ReadAsync<ListDetail>(await client.PostAsJsonAsync("/api/lists", new { name }));
        return created.Id;
    }
}
