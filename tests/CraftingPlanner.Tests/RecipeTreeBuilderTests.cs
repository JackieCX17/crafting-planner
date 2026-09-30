using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Services;
using static CraftingPlanner.Tests.SampleRecipes;

namespace CraftingPlanner.Tests;

/// <summary>Checks for <see cref="RecipeTreeBuilder"/>.</summary>
public class RecipeTreeBuilderTests
{
    /// <summary>Name of every sample item, by id, as the builder needs it.</summary>
    private static readonly Dictionary<int, string> AllNames =
        Names.Select((name, index) => (name, id: index + 1)).ToDictionary(pair => pair.id, pair => pair.name);

    /// <summary>The top node describes the item asked for, with a quantity of 1.</summary>
    [Fact]
    public void Root_IsTheItemAskedFor()
    {
        var tree = RecipeTreeBuilder.Build(Id("Tool Kit"), Recipes, AllNames);

        Assert.Equal("Tool Kit", tree.ItemName);
        Assert.Equal(ItemKind.Crafted, tree.Kind);
        Assert.Equal(1, tree.Quantity);
        Assert.Equal(1, tree.OutputQuantity);
        Assert.Equal(4, tree.CraftSeconds);
        Assert.False(tree.Truncated);
    }

    /// <summary>Each node's ingredients are the recipe's ingredients, with the quantity one craft uses.</summary>
    [Fact]
    public void Nodes_ListTheRecipeIngredientsWithTheirQuantities()
    {
        var tree = RecipeTreeBuilder.Build(Id("Sword"), Recipes, AllNames);

        Assert.Equal(
            [("Iron Ingot", 2), ("Stick", 1)],
            tree.Ingredients.Select(node => (node.ItemName, node.Quantity)).OrderBy(pair => pair.ItemName));

        var stick = tree.Ingredients.Single(node => node.ItemName == "Stick");
        var plank = Assert.Single(stick.Ingredients);
        Assert.Equal(("Plank", 2), (plank.ItemName, plank.Quantity));

        var log = Assert.Single(plank.Ingredients);
        Assert.Equal(ItemKind.Raw, log.Kind);
        Assert.Null(log.OutputQuantity);
        Assert.Empty(log.Ingredients);
    }

    /// <summary>A shared item appears under every recipe that uses it, because the tree shows structure.</summary>
    [Fact]
    public void SharedItems_AppearUnderEachUser()
    {
        var tree = RecipeTreeBuilder.Build(Id("Tool Kit"), Recipes, AllNames);

        var sticks = tree.Ingredients
            .SelectMany(node => node.Ingredients)
            .Where(node => node.ItemName == "Stick")
            .ToList();

        Assert.Equal(2, sticks.Count);
        Assert.Equal([1, 2], sticks.Select(node => node.Quantity).OrderBy(quantity => quantity));
    }

    /// <summary>A raw item's tree is a single node.</summary>
    [Fact]
    public void RawItem_IsASingleNode()
    {
        var tree = RecipeTreeBuilder.Build(Id("Log"), Recipes, AllNames);

        Assert.Equal(ItemKind.Raw, tree.Kind);
        Assert.Empty(tree.Ingredients);
        Assert.False(tree.Truncated);
    }

    /// <summary>
    /// When the size limit is reached, the deepest nodes are left unexpanded and marked, and
    /// the nodes above them are complete.
    /// </summary>
    [Fact]
    public void SizeLimit_TruncatesTheDeepestNodesAndMarksThem()
    {
        // Tool Kit (1) + Sword and Pickaxe (3) + their four ingredients (7). A limit of 7
        // allows exactly those, so Sword's and Pickaxe's crafted ingredients are marked.
        var tree = RecipeTreeBuilder.Build(Id("Tool Kit"), Recipes, AllNames, nodeLimit: 7);

        Assert.Equal(7, CountNodes(tree));
        Assert.False(tree.Truncated);
        Assert.All(tree.Ingredients, node => Assert.False(node.Truncated));

        var grandchildren = tree.Ingredients.SelectMany(node => node.Ingredients).ToList();
        Assert.Equal(4, grandchildren.Count);
        Assert.All(grandchildren, node =>
        {
            Assert.True(node.Truncated, $"{node.ItemName} should be marked as truncated.");
            Assert.Empty(node.Ingredients);
        });
    }

    /// <summary>Counts every node in a tree.</summary>
    /// <param name="node">The top node.</param>
    /// <returns>The count, including the top node.</returns>
    private static int CountNodes(TreeNode node) => 1 + node.Ingredients.Sum(CountNodes);
}
