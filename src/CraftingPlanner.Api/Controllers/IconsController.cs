using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CraftingPlanner.Api.Controllers;

/// <summary>Endpoints for the pictures an item can be given.</summary>
/// <param name="catalog">The icons the website ships with. Supplied automatically.</param>
[Route("api/icons")]
[Tags("Icons")]
public class IconsController(IconCatalog catalog) : PlannerControllerBase
{
    /// <summary>Lists the pictures an item can be given.</summary>
    /// <remarks>
    /// An item's <c>icon</c> field must be one of these names. The icons come from
    /// game-icons.net under the Creative Commons Attribution 3.0 license, and each entry
    /// names its author.
    /// </remarks>
    /// <returns>The icons, in name order.</returns>
    /// <response code="200">The icons.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<IconInfo>>(StatusCodes.Status200OK, Json)]
    public ActionResult<IReadOnlyList<IconInfo>> List() => Ok(catalog.Icons);
}
