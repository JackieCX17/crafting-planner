using System.Net;
using System.Net.Http.Json;
using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Services;
using static CraftingPlanner.Tests.PlannerApp;

namespace CraftingPlanner.Tests;

/// <summary>
/// Checks the public demo's guard rails. These start their own app with demo mode on, so
/// the ordinary tests are not affected by the rate limit.
/// </summary>
public class DemoModeTests
{
    /// <summary>The about endpoint says demo mode is on, and the sample data is present after the reset.</summary>
    [Fact]
    public async Task DemoMode_IsReportedAndStartsWithSampleData()
    {
        using var app = new PlannerApp { DemoMode = true };
        var client = app.CreateClient();

        var about = await client.GetFromJsonAsync<AboutResponse>("/api/about", Json);
        var stats = await client.GetFromJsonAsync<StatsResponse>("/api/stats", Json);

        Assert.True(about!.DemoMode);
        Assert.Equal(19, stats!.ItemCount);
    }

    /// <summary>
    /// Past the per-minute limit, API requests are refused with 429 in the standard error
    /// format. Pages and pictures are not limited.
    /// </summary>
    [Fact]
    public async Task DemoMode_LimitsApiRequestsButNotPages()
    {
        using var app = new PlannerApp { DemoMode = true };
        var client = app.CreateClient();

        HttpResponseMessage? refused = null;
        for (var i = 0; i < DemoMode.RequestsPerMinute + 5; i++)
        {
            var response = await client.GetAsync("/api/about");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                refused = response;
                break;
            }
        }

        Assert.NotNull(refused);
        Assert.Equal("application/problem+json", refused.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Too many requests", (string?)(await BodyAsync(refused))["title"]);

        var page = await client.GetAsync("/index.html");
        var icon = await client.GetAsync("/icons/broadsword.svg");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, icon.StatusCode);
    }

    /// <summary>A request body far larger than any real one is refused rather than read.</summary>
    [Fact]
    public async Task DemoMode_RefusesAHugeRequestBody()
    {
        using var app = new PlannerApp { DemoMode = true };
        var client = app.CreateClient();

        var huge = new StringContent("{\"name\":\"" + new string('x', 200_000) + "\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/items", huge);

        Assert.NotEqual(HttpStatusCode.Created, response.StatusCode);
    }
}
