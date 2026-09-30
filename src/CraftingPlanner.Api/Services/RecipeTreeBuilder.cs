using CraftingPlanner.Api.Contracts;

namespace CraftingPlanner.Api.Services;

/// <summary>
/// Builds the recipe tree of an item for display: the item, its ingredients, their
/// ingredients, and so on down to raw materials.
/// </summary>
/// <remarks>
/// A tree repeats shared items. Stick appears under Sword and again under Pickaxe, so a
/// wide, deep set of recipes could produce an enormous tree. The builder therefore stops
/// expanding once it has made a set number of nodes, and marks the nodes it left
/// unexpanded. The tree is built level by level with a queue, so the size limit lands on
/// the deepest levels, and a long chain cannot exhaust the stack.
/// </remarks>
public static class RecipeTreeBuilder
{
    /// <summary>The largest number of nodes a tree is allowed to have.</summary>
    public const int DefaultNodeLimit = 2_000;

    /// <summary>Builds the tree for an item.</summary>
    /// <param name="rootItemId">Id of the item at the top.</param>
    /// <param name="recipes">Every recipe, by the id of the item it makes. An absent item is raw.</param>
    /// <param name="names">Name of every item that can appear in the tree, by id.</param>
    /// <param name="nodeLimit">The largest number of nodes to make.</param>
    /// <returns>The top node, with everything below it nested inside.</returns>
    public static TreeNode Build(
        int rootItemId,
        IReadOnlyDictionary<int, PlanRecipe> recipes,
        IReadOnlyDictionary<int, string> names,
        int nodeLimit = DefaultNodeLimit)
    {
        var root = NewNode(rootItemId, quantity: 1, recipes, names);
        var nodesMade = 1;

        // Nodes whose ingredients have not been added yet, nearest the top first.
        var toExpand = new Queue<TreeNode>();
        toExpand.Enqueue(root);

        while (toExpand.Count > 0)
        {
            var node = toExpand.Dequeue();
            if (!recipes.TryGetValue(node.ItemId, out var recipe))
            {
                continue;
            }

            if (nodesMade + recipe.Ingredients.Count > nodeLimit)
            {
                node.Truncated = true;
                continue;
            }

            foreach (var ingredient in recipe.Ingredients)
            {
                var child = NewNode(ingredient.ItemId, ingredient.Quantity, recipes, names);
                node.Ingredients.Add(child);
                toExpand.Enqueue(child);
                nodesMade++;
            }
        }

        return root;
    }

    /// <summary>Makes one node, without its ingredients.</summary>
    /// <param name="itemId">Id of the item.</param>
    /// <param name="quantity">Units one craft of the item above uses.</param>
    /// <param name="recipes">Every recipe, by the id of the item it makes.</param>
    /// <param name="names">Name of every item, by id.</param>
    /// <returns>The node.</returns>
    private static TreeNode NewNode(
        int itemId,
        int quantity,
        IReadOnlyDictionary<int, PlanRecipe> recipes,
        IReadOnlyDictionary<int, string> names)
    {
        recipes.TryGetValue(itemId, out var recipe);

        return new TreeNode
        {
            ItemId = itemId,
            ItemName = names[itemId],
            Kind = recipe is null ? ItemKind.Raw : ItemKind.Crafted,
            Quantity = quantity,
            OutputQuantity = recipe?.OutputQuantity,
            CraftSeconds = recipe?.CraftSeconds,
        };
    }
}
