using System.Net;
using System.Net.Http.Json;
using CraftingPlanner.Api.Contracts;
using static CraftingPlanner.Tests.PlannerApp;

namespace CraftingPlanner.Tests;

/// <summary>
/// Calls the planning, summary, documentation, and website endpoints on the running app.
/// These tests only read, so they share one app and one database freely.
/// </summary>
/// <param name="app">The app, started once for the class.</param>
public class PlanningEndpointTests(PlannerApp app) : IClassFixture<PlannerApp>
{
    private readonly HttpClient client = app.CreateClient();

    /// <summary>Three Tool Kits through the endpoint give the design document's numbers, with names attached.</summary>
    [Fact]
    public async Task Plan_MatchesTheDesignDocument()
    {
        var toolKit = await IdOfAsync(client, "Tool Kit");

        var plan = await client.GetFromJsonAsync<PlanResponse>($"/api/items/{toolKit}/plan?quantity=3", Json);

        Assert.NotNull(plan);
        Assert.Equal("Tool Kit", plan.ItemName);
        Assert.Equal([("Coal", 15L), ("Iron Ore", 30L), ("Log", 3L)], plan.RawMaterials.Select(raw => (raw.ItemName, raw.Quantity)));
        Assert.Equal(["Plank", "Iron Ingot", "Stick", "Pickaxe", "Sword", "Tool Kit"], plan.Steps.Select(step => step.ItemName));
        Assert.Equal(222, plan.TotalSeconds);

        var stick = plan.Steps.Single(step => step.ItemName == "Stick");
        Assert.Equal((9L, 3L, 12L, 3L), (stick.Needed, stick.Crafts, stick.Made, stick.Leftover));
    }

    /// <summary>A quantity outside 1 to 1,000,000 is refused with a plain message.</summary>
    [Fact]
    public async Task Plan_RefusesAQuantityOutOfRange()
    {
        var toolKit = await IdOfAsync(client, "Tool Kit");

        var response = await client.GetAsync($"/api/items/{toolKit}/plan?quantity=0");
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("The quantity must be 1 to 1,000,000.", body["errors"]!["quantity"]!.AsArray().Select(node => (string)node!));
    }

    /// <summary>The export is a CSV download named after the item, starting with the UTF-8 marker and the header.</summary>
    [Fact]
    public async Task Export_IsACsvDownload()
    {
        var toolKit = await IdOfAsync(client, "Tool Kit");

        var response = await client.GetAsync($"/api/items/{toolKit}/plan/export?quantity=3");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("plan-tool-kit-x3.csv", response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.StartsWith("﻿Type,Item,Quantity,Crafts,Made,Leftover,Seconds\r\nRaw material,Coal,15,,,,\r\n", text);
        Assert.EndsWith("Total time,,,,,,222\r\n", text);
    }

    /// <summary>The tree of the Sword reaches the raw materials, with per-craft quantities.</summary>
    [Fact]
    public async Task Tree_ReachesTheRawMaterials()
    {
        var sword = await IdOfAsync(client, "Sword");

        var tree = await client.GetFromJsonAsync<TreeNode>($"/api/items/{sword}/tree", Json);

        Assert.NotNull(tree);
        Assert.Equal("Sword", tree.ItemName);
        var stick = tree.Ingredients.Single(node => node.ItemName == "Stick");
        var plank = Assert.Single(stick.Ingredients);
        var log = Assert.Single(plank.Ingredients);
        Assert.Equal((2, ItemKind.Crafted), (plank.Quantity, plank.Kind));
        Assert.Equal((1, ItemKind.Raw), (log.Quantity, log.Kind));
        Assert.Empty(log.Ingredients);
    }

    /// <summary>The summary matches a hand count of the sample data.</summary>
    [Fact]
    public async Task Stats_MatchTheSampleData()
    {
        var stats = await client.GetFromJsonAsync<StatsResponse>("/api/stats", Json);

        Assert.NotNull(stats);
        Assert.Equal((19, 7, 12, 6, 22), (stats.ItemCount, stats.RawItemCount, stats.CraftedItemCount, stats.CategoryCount, stats.IngredientLineCount));
        Assert.Equal(("Stick", 5), (stats.MostUsedIngredient?.ItemName, stats.MostUsedIngredient?.RecipeCount));
        Assert.Equal(("Hunter Kit", 4), (stats.LongestChain?.ItemName, stats.LongestChain?.Depth));
    }

    /// <summary>The API description lists every endpoint, and each one carries a summary from the code comments.</summary>
    [Fact]
    public async Task OpenApi_DescribesEveryEndpointWithASummary()
    {
        var document = await client.GetFromJsonAsync<System.Text.Json.Nodes.JsonNode>("/openapi/v1.json");

        var operations = document!["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(method => (Path: path.Key, Method: method.Key, Operation: method.Value)))
            .ToList();

        Assert.Equal(19, operations.Count);
        Assert.All(operations, operation => Assert.False(
            string.IsNullOrWhiteSpace((string?)operation.Operation!["summary"]),
            $"{operation.Method.ToUpperInvariant()} {operation.Path} has no summary"));
    }

    /// <summary>The website is served from the root, and the interactive API page from /swagger.</summary>
    [Fact]
    public async Task Website_AndApiPage_AreServed()
    {
        var home = await client.GetAsync("/");
        var plan = await client.GetAsync("/plan.html");
        var script = await client.GetAsync("/js/api.js");
        var swagger = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("Crafting Planner", await home.Content.ReadAsStringAsync());
        Assert.Equal("text/html", home.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, plan.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
    }

    /// <summary>The icon list names each shipped picture with its author and address, and the sample items use them.</summary>
    [Fact]
    public async Task Icons_AreListedWithAuthors_AndSampleItemsUseThem()
    {
        var icons = await client.GetFromJsonAsync<List<IconInfo>>("/api/icons", Json);
        var sword = await client.GetFromJsonAsync<ItemDetail>($"/api/items/{await IdOfAsync(client, "Sword")}", Json);
        var file = await client.GetAsync("/icons/broadsword.svg");

        Assert.NotNull(icons);
        var broadsword = icons.Single(icon => icon.Name == "broadsword");
        Assert.Equal(("Lorc", "/icons/broadsword.svg"), (broadsword.Author, broadsword.Url));
        Assert.Equal("broadsword", sword?.Icon);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/svg+xml", file.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>An address that does not exist is answered in the standard error format, not with an empty page.</summary>
    [Fact]
    public async Task UnknownAddress_IsAStandardRejection()
    {
        var response = await client.GetAsync("/api/nothing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
