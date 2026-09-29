namespace CraftingPlanner.Api.Services;

/// <summary>
/// Finds recipes that would loop back on themselves, such as a Stick that needs a Plank
/// while a Plank needs a Stick. A loop can never be crafted, and it would make the planning
/// calculation run forever, so a recipe that creates one is refused when it is saved.
/// </summary>
/// <remarks>
/// This class works only with plain numbers and lists. It never touches the database or
/// the web, so it can be tested on its own.
/// </remarks>
public static class RecipeLoopFinder
{
    /// <summary>
    /// Checks whether giving an item these ingredients would create a loop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The search starts at the ingredients and follows recipes downwards: each ingredient's
    /// own ingredients, then theirs, and so on. If it ever arrives back at the item the
    /// recipe makes, there is a loop.
    /// </para>
    /// <para>
    /// Items are visited nearest first, so the loop that is returned is the shortest one.
    /// The search uses a queue, not a function that calls itself, so a very long chain of
    /// recipes cannot exhaust the program's memory for nested calls.
    /// </para>
    /// </remarks>
    /// <param name="outputItemId">Id of the item the recipe makes.</param>
    /// <param name="ingredientIds">Ids of the items the recipe would use.</param>
    /// <param name="ingredientsByItem">
    /// Every other recipe, as "id of the item it makes" to "ids of the items it uses".
    /// Raw items are absent. The recipe being checked must be left out.
    /// </param>
    /// <returns>
    /// The loop as a chain of item ids in which each item needs the next, starting and ending
    /// with <paramref name="outputItemId"/>. Null when there is no loop.
    /// </returns>
    public static IReadOnlyList<int>? FindLoop(
        int outputItemId,
        IEnumerable<int> ingredientIds,
        IReadOnlyDictionary<int, IReadOnlyList<int>> ingredientsByItem)
    {
        // For each item reached, the item that led to it. This doubles as the record
        // of what has been visited, and it is what the chain is rebuilt from.
        var cameFrom = new Dictionary<int, int>();
        var toVisit = new Queue<int>();

        foreach (var ingredientId in ingredientIds)
        {
            if (cameFrom.TryAdd(ingredientId, outputItemId))
            {
                toVisit.Enqueue(ingredientId);
            }
        }

        while (toVisit.Count > 0)
        {
            var current = toVisit.Dequeue();

            if (current == outputItemId)
            {
                return BuildChain(outputItemId, cameFrom);
            }

            if (!ingredientsByItem.TryGetValue(current, out var itsIngredients))
            {
                continue;
            }

            foreach (var next in itsIngredients)
            {
                if (cameFrom.TryAdd(next, current))
                {
                    toVisit.Enqueue(next);
                }
            }
        }

        return null;
    }

    /// <summary>Rebuilds the loop by walking backwards from the end to the start.</summary>
    /// <param name="outputItemId">Id of the item the loop starts and ends with.</param>
    /// <param name="cameFrom">For each item reached, the item that led to it.</param>
    /// <returns>The loop in forward order.</returns>
    private static List<int> BuildChain(int outputItemId, Dictionary<int, int> cameFrom)
    {
        var chain = new List<int> { outputItemId };

        var current = cameFrom[outputItemId];
        while (current != outputItemId)
        {
            chain.Add(current);
            current = cameFrom[current];
        }

        chain.Add(outputItemId);
        chain.Reverse();

        return chain;
    }
}
