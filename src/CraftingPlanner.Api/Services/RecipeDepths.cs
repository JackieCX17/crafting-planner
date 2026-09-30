namespace CraftingPlanner.Api.Services;

/// <summary>
/// Works out how long each item's recipe chain is: a raw item is 0, and a crafted item is
/// one more than its deepest ingredient. A Tool Kit made from a Sword made from a Stick
/// made from a Plank made from a Log has a depth of 4.
/// </summary>
/// <remarks>
/// Items are processed ingredients first, so that every ingredient's depth is known when its
/// user's turn comes. That order is built the same way the planning calculation builds its
/// order, by counting and using a queue, and for the same reason: no recursion on user data.
/// </remarks>
public static class RecipeDepths
{
    /// <summary>Works out the depth of every item that appears in any recipe.</summary>
    /// <param name="recipes">Every recipe, by the id of the item it makes.</param>
    /// <returns>Item id to depth. Raw items that no recipe uses are absent.</returns>
    /// <exception cref="InvalidOperationException">The recipes contain a loop.</exception>
    public static IReadOnlyDictionary<int, int> Compute(IReadOnlyDictionary<int, PlanRecipe> recipes)
    {
        // For each item, the crafted items whose recipes use it.
        var usedBy = new Dictionary<int, List<int>>();

        // For each crafted item, how many of its ingredients still have no depth.
        var ingredientsLeft = new Dictionary<int, int>();

        var depth = new Dictionary<int, int>();
        var ready = new Queue<int>();

        foreach (var (itemId, recipe) in recipes)
        {
            ingredientsLeft[itemId] = recipe.Ingredients.Count;

            // The API never saves a recipe without ingredients, but if one existed it
            // would be one step above nothing at all.
            if (recipe.Ingredients.Count == 0)
            {
                depth[itemId] = 1;
                ready.Enqueue(itemId);
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                if (!usedBy.TryGetValue(ingredient.ItemId, out var users))
                {
                    users = [];
                    usedBy[ingredient.ItemId] = users;
                }

                users.Add(itemId);

                // A raw ingredient has depth 0 and is ready straight away.
                if (!recipes.ContainsKey(ingredient.ItemId) && depth.TryAdd(ingredient.ItemId, 0))
                {
                    ready.Enqueue(ingredient.ItemId);
                }
            }
        }

        while (ready.Count > 0)
        {
            var itemId = ready.Dequeue();
            if (!usedBy.TryGetValue(itemId, out var users))
            {
                continue;
            }

            foreach (var user in users)
            {
                if (--ingredientsLeft[user] == 0)
                {
                    depth[user] = 1 + recipes[user].Ingredients.Max(ingredient => depth[ingredient.ItemId]);
                    ready.Enqueue(user);
                }
            }
        }

        if (recipes.Keys.Any(itemId => !depth.ContainsKey(itemId)))
        {
            throw new InvalidOperationException("The recipes contain a loop, so their depths cannot be worked out.");
        }

        return depth;
    }
}
