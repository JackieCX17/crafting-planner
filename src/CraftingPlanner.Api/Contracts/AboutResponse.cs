namespace CraftingPlanner.Api.Contracts;

/// <summary>Facts about the running app itself.</summary>
public class AboutResponse
{
    /// <summary>The app's name.</summary>
    /// <example>Crafting Planner</example>
    public required string Name { get; init; }

    /// <summary>The app's version.</summary>
    /// <example>1.0.0</example>
    public required string Version { get; init; }

    /// <summary>
    /// True on the public demo: the data resets to the sample set whenever the app starts,
    /// and the API is rate-limited. False when the app is run locally.
    /// </summary>
    public required bool DemoMode { get; init; }

    /// <summary>Where the source code lives.</summary>
    /// <example>https://github.com/JackieCX17/crafting-planner</example>
    public required string Source { get; init; }
}
