using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CraftingPlanner.Tests;

/// <summary>
/// Starts the real app in memory, on its own fresh database file, so tests can call the
/// endpoints the way any client would. Each instance gets a new database with the sample
/// data, and deletes it when it is disposed.
/// </summary>
public sealed class PlannerApp : WebApplicationFactory<Program>
{
    /// <summary>The database file this instance uses, in the system's temporary folder.</summary>
    private readonly string databaseFile =
        Path.Combine(Path.GetTempPath(), $"craftingplanner-test-{Guid.NewGuid():N}.db");

    /// <summary>
    /// Whether to start the app with the public demo's guard rails on. Set it when creating
    /// the instance; the app is only started on the first request for a client.
    /// </summary>
    public bool DemoMode { get; init; }

    /// <summary>
    /// The JSON rules the API uses, so responses read back into the API's own response classes.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Points the app at this instance's own database file.</summary>
    /// <param name="builder">The app's host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DatabaseFile", databaseFile);
        builder.UseSetting("DemoMode", DemoMode ? "true" : "false");
    }

    /// <summary>Stops the app and deletes its database file.</summary>
    /// <param name="disposing">True when called from Dispose.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // SQLite keeps connections open in a pool for reuse. They have to be closed
        // before the file can be deleted. Only this database's pool is cleared: clearing
        // every pool in the process would cut off connections that other test classes,
        // running at the same time on their own databases, are in the middle of using.
        Microsoft.Data.Sqlite.SqliteConnection.ClearPool(
            new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databaseFile}"));

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var file = databaseFile + suffix;
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }

    /// <summary>Finds a sample item's id by its exact name.</summary>
    /// <param name="client">A client for the app.</param>
    /// <param name="name">The item's name.</param>
    /// <returns>The id.</returns>
    public static async Task<int> IdOfAsync(HttpClient client, string name)
    {
        var page = await client.GetFromJsonAsync<JsonNode>($"/api/items?search={Uri.EscapeDataString(name)}&pageSize=100");
        var match = page!["items"]!.AsArray().Single(item => (string)item!["name"]! == name);
        return (int)match!["id"]!;
    }

    /// <summary>
    /// Reads a response body into one of the API's response classes. When the body is not that
    /// shape, for example because the request was rejected, the failure says what came back.
    /// </summary>
    /// <typeparam name="T">The response class expected.</typeparam>
    /// <param name="response">The response.</param>
    /// <returns>The body.</returns>
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonSerializer.Deserialize<T>(text, Json)!;
        }
        catch (JsonException error)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected a {typeof(T).Name} but the response was {(int)response.StatusCode} {response.StatusCode}: {text}",
                error);
        }
    }

    /// <summary>Reads a response body as a JSON tree, for checks on rejections and loose shapes.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The body.</returns>
    public static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
