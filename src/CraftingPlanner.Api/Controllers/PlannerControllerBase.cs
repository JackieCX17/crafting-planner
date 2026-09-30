using Microsoft.AspNetCore.Mvc;

namespace CraftingPlanner.Api.Controllers;

/// <summary>
/// Behaviour shared by every controller in the API.
/// </summary>
/// <remarks>
/// <c>[ApiController]</c> switches on automatic checking of incoming data. A request that
/// breaks a rule declared on a request class, such as a name that is too long, is answered
/// with 400 before the endpoint's own code runs.
/// </remarks>
[ApiController]
public abstract class PlannerControllerBase : ControllerBase
{
    /// <summary>Content type of every rejection, as defined by the Problem Details standard.</summary>
    protected const string ProblemJson = "application/problem+json";

    /// <summary>Content type of every successful response that has a body.</summary>
    protected const string Json = "application/json";

    /// <summary>
    /// Builds a rejection in the standard error format (Problem Details).
    /// </summary>
    /// <param name="statusCode">The HTTP status code, such as 404 or 409.</param>
    /// <param name="title">A short name for the kind of problem. The same for every case of it.</param>
    /// <param name="detail">A plain explanation of this particular case.</param>
    /// <param name="extraName">Name of one extra field to include, or null for none.</param>
    /// <param name="extraValue">Value of the extra field.</param>
    /// <returns>The response to send back.</returns>
    protected ObjectResult Rejection(
        int statusCode,
        string title,
        string detail,
        string? extraName = null,
        object? extraValue = null)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode,
            title,
            detail: detail);

        if (extraName is not null)
        {
            problem.Extensions[extraName] = extraValue;
        }

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { ProblemJson },
        };
    }

    /// <summary>Builds a 404 rejection for a record that does not exist.</summary>
    /// <param name="what">The kind of record, such as "item".</param>
    /// <param name="id">The id that was asked for.</param>
    /// <returns>The response to send back.</returns>
    protected ObjectResult NotFoundRejection(string what, int id) =>
        Rejection(
            StatusCodes.Status404NotFound,
            $"The {what} was not found",
            $"There is no {what} with id {id}.");

    /// <summary>
    /// Tidies optional text: removes spaces at both ends, and treats text that is
    /// empty or only spaces as "no value".
    /// </summary>
    /// <param name="text">The text as it was sent.</param>
    /// <returns>The tidied text, or null when there was nothing in it.</returns>
    protected static string? Tidy(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
