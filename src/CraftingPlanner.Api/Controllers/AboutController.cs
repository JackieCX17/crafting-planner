using System.Reflection;
using CraftingPlanner.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CraftingPlanner.Api.Controllers;

/// <summary>Endpoints about the running app itself.</summary>
/// <param name="configuration">The app's settings. Supplied automatically.</param>
[Route("api/about")]
[Tags("Summary")]
public class AboutController(IConfiguration configuration) : PlannerControllerBase
{
    /// <summary>Where the source code lives.</summary>
    public const string SourceUrl = "https://github.com/JackieCX17/crafting-planner";

    /// <summary>Says what the app is, which version is running, and whether it is the public demo.</summary>
    /// <remarks>
    /// The website reads this to show a notice on the public demo. It is also the address the
    /// host checks to see that the app is up.
    /// </remarks>
    /// <returns>The facts.</returns>
    /// <response code="200">The facts.</response>
    [HttpGet]
    [ProducesResponseType<AboutResponse>(StatusCodes.Status200OK, Json)]
    public ActionResult<AboutResponse> Get() =>
        new AboutResponse
        {
            Name = "Crafting Planner",
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown",
            DemoMode = configuration.GetValue<bool>("DemoMode"),
            Source = SourceUrl,
        };
}
