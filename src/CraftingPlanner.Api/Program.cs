using System.Text.Json;
using System.Text.Json.Serialization;
using CraftingPlanner.Api.Data;
using CraftingPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;

// Program.cs is where the app starts. The first half registers the parts the app
// is built from. The second half decides how each incoming request is handled.

var builder = WebApplication.CreateBuilder(args);

// Controllers hold the endpoints. See the Controllers folder.
builder.Services
    .AddControllers(options =>
        // Name the fields in error messages the way they are spelled in JSON ("name", not "Name").
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => ApplyJsonRules(options.JsonSerializerOptions));

// The interactive API page keeps its own copy of the JSON settings, so the same rules are applied there.
builder.Services.ConfigureHttpJsonOptions(options => ApplyJsonRules(options.SerializerOptions));

// Every rejection uses the standard error format (Problem Details).
builder.Services.AddProblemDetails();

// The database is a single file that sits next to the app.
var databaseFile = Path.Combine(
    builder.Environment.ContentRootPath,
    builder.Configuration["DatabaseFile"] ?? "craftingplanner.db");

builder.Services.AddDbContext<PlannerDbContext>(options =>
    options.UseSqlite($"Data Source={databaseFile}"));

// The pictures an item can be given: read from the website's icons folder once.
builder.Services.AddSingleton<IconCatalog>();

// The public demo gets extra guard rails; a local run does not. See Services/DemoMode.cs.
var demoMode = DemoMode.IsOn(builder.Configuration);
if (demoMode)
{
    DemoMode.AddServices(builder);
}

// The API description that the interactive page is drawn from.
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info = new()
        {
            Title = "Crafting Planner API",
            Version = "v1",
            Description =
                "Stores items and recipes, and works out the raw materials needed to craft any item. "
                + "Every rejection uses the Problem Details format: a title, a plain explanation in "
                + "\"detail\", and the fields at fault in \"errors\" where that applies.",
        };

        return Task.CompletedTask;
    }));

var app = builder.Build();

// On first run, create the database tables and add the sample data. On later runs, make
// sure the file still matches the code, and stop with a plain message if it does not.
using (var scope = app.Services.CreateScope())
{
    if (demoMode)
    {
        DemoMode.ResetDatabase(databaseFile, app.Logger);
    }

    var db = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
    var created = db.Database.EnsureCreated();

    if (!created)
    {
        var missing = SchemaCheck.FindMissingColumns(db);
        if (missing.Count > 0)
        {
            app.Logger.LogCritical(
                "The database file {File} was made by an older version of the app and lacks: {Missing}. "
                + "Stop the app, delete the file, and start again. The sample data comes back.",
                databaseFile,
                string.Join(", ", missing));
            return 1;
        }
    }

    SeedData.AddIfEmpty(db);
}

// Unexpected failures and unknown addresses are answered in the standard error format too.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (demoMode)
{
    DemoMode.Use(app);
}

// The interactive API page is part of what this project delivers,
// so it is switched on everywhere, not only during development.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Crafting Planner API");
    options.DocumentTitle = "Crafting Planner API";
});

// The website is plain files in the wwwroot folder. "/" opens index.html.
// The pages reach the backend only through the API, like any other caller.
app.UseDefaultFiles();
app.UseStaticFiles();

// On the demo, the API endpoints are rate-limited; the pages and pictures are not.
var endpoints = app.MapControllers();
if (demoMode)
{
    endpoints.RequireRateLimiting(DemoMode.ApiPolicy);
}

app.Run();

return 0;

// The rules for reading and writing JSON.
static void ApplyJsonRules(JsonSerializerOptions json)
{
    // Choices such as "kind" are sent as words ("raw", "crafted"), not as numbers.
    json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

    // Numbers must be sent as numbers. A number inside quotes is refused.
    json.NumberHandling = JsonNumberHandling.Strict;
}
