namespace CraftingPlanner.Api.Services;

/// <summary>
/// Works out the raw materials, crafting steps, and leftovers for a quantity of an item.
/// Section 5 of docs/DESIGN.md describes the steps and works through an example.
/// </summary>
/// <remarks>
/// <para>
/// This is the C# version of <c>prototype/plan.py</c>, checked against the same cases.
/// It works in two passes. The first pass walks the recipes and produces only an order
/// (steps 1 and 2 of the design). The second pass does the arithmetic (steps 3 to 5).
/// Keeping them apart is what stops a shared ingredient from being rounded separately
/// for each branch that needs it.
/// </para>
/// <para>
/// The prototype builds its order with a function that calls itself. This version counts
/// instead, with a queue, so a very long chain of recipes cannot exhaust the program's
/// memory for nested calls. The loop search in <see cref="RecipeLoopFinder"/> makes the
/// same choice for the same reason.
/// </para>
/// </remarks>
public static class PlanCalculator
{
    /// <summary>Works out the plan for a quantity of an item.</summary>
    /// <param name="targetItemId">Id of the item to make.</param>
    /// <param name="quantity">How many units of it to make. 1 or more.</param>
    /// <param name="recipes">
    /// Every recipe, as "id of the item it makes" to the recipe. An item that is absent is raw.
    /// The recipes must contain no loops; the API refuses those when they are saved.
    /// </param>
    /// <returns>The plan.</returns>
    /// <exception cref="PlanTooLargeException">A total would not fit in a 64-bit whole number.</exception>
    /// <exception cref="InvalidOperationException">The recipes contain a loop.</exception>
    public static PlanResult Calculate(
        int targetItemId,
        long quantity,
        IReadOnlyDictionary<int, PlanRecipe> recipes)
    {
        var order = OrderTargetFirst(targetItemId, recipes);

        // A running total of the units needed of each item. By the time an item's turn
        // comes, every item that uses it has already added its share.
        var demand = new Dictionary<int, long> { [targetItemId] = quantity };
        var steps = new List<PlanStep>();
        var rawMaterials = new List<RawMaterial>();
        long totalSeconds = 0;

        foreach (var itemId in order)
        {
            var needed = demand[itemId];

            if (!recipes.TryGetValue(itemId, out var recipe))
            {
                // Raw: its demand is the final answer.
                rawMaterials.Add(new RawMaterial(itemId, needed));
                continue;
            }

            try
            {
                // "checked" makes the arithmetic throw instead of silently wrapping around
                // when a number no longer fits.
                checked
                {
                    // Divide, rounding up, using whole numbers only.
                    var crafts = (needed + recipe.OutputQuantity - 1) / recipe.OutputQuantity;
                    var made = crafts * recipe.OutputQuantity;
                    var seconds = crafts * recipe.CraftSeconds;

                    steps.Add(new PlanStep(itemId, needed, crafts, made, Leftover: made - needed, seconds));
                    totalSeconds += seconds;

                    foreach (var ingredient in recipe.Ingredients)
                    {
                        demand[ingredient.ItemId] =
                            demand.GetValueOrDefault(ingredient.ItemId) + crafts * ingredient.Quantity;
                    }
                }
            }
            catch (OverflowException)
            {
                throw new PlanTooLargeException(itemId);
            }
        }

        // Steps were added target first. A person crafting wants raw-most first.
        steps.Reverse();

        return new PlanResult(rawMaterials, steps, totalSeconds);
    }

    /// <summary>
    /// Every item the target depends on, in an order where each item comes before its own
    /// ingredients. The target is first.
    /// </summary>
    /// <remarks>
    /// First the items are collected by walking from the target through the recipes.
    /// Then each item is given a count of how many of the collected items use it. An item
    /// whose count is zero is ready: nothing left unprocessed needs it. Processing an item
    /// lowers the count of each of its ingredients, and any that reach zero become ready.
    /// This is a standard method called a topological sort.
    /// </remarks>
    /// <param name="targetItemId">Id of the item to make.</param>
    /// <param name="recipes">Every recipe, by the id of the item it makes.</param>
    /// <returns>The item ids in processing order.</returns>
    /// <exception cref="InvalidOperationException">The recipes contain a loop.</exception>
    private static List<int> OrderTargetFirst(int targetItemId, IReadOnlyDictionary<int, PlanRecipe> recipes)
    {
        // Step 1: collect every item the target depends on, at any depth.
        var involved = new HashSet<int>();
        var toExplore = new Stack<int>();
        toExplore.Push(targetItemId);

        while (toExplore.Count > 0)
        {
            var itemId = toExplore.Pop();
            if (!involved.Add(itemId) || !recipes.TryGetValue(itemId, out var recipe))
            {
                continue;
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                toExplore.Push(ingredient.ItemId);
            }
        }

        // Step 2: count, for each item, how many of the involved items use it.
        var usersLeft = involved.ToDictionary(itemId => itemId, _ => 0);
        foreach (var itemId in involved)
        {
            if (recipes.TryGetValue(itemId, out var recipe))
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    usersLeft[ingredient.ItemId]++;
                }
            }
        }

        // Items nothing needs are ready. Without loops, that is only the target.
        var ready = new Queue<int>(involved.Where(itemId => usersLeft[itemId] == 0));
        var order = new List<int>(involved.Count);

        while (ready.Count > 0)
        {
            var itemId = ready.Dequeue();
            order.Add(itemId);

            if (!recipes.TryGetValue(itemId, out var recipe))
            {
                continue;
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                usersLeft[ingredient.ItemId]--;
                if (usersLeft[ingredient.ItemId] == 0)
                {
                    ready.Enqueue(ingredient.ItemId);
                }
            }
        }

        // Every involved item should have been reached. If some were not, they are in a
        // loop, which the API should have refused when the recipe was saved.
        if (order.Count != involved.Count)
        {
            throw new InvalidOperationException("The recipes contain a loop, so no plan can be made.");
        }

        return order;
    }
}
