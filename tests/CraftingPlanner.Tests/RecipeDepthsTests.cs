using CraftingPlanner.Api.Services;
using static CraftingPlanner.Tests.SampleRecipes;

namespace CraftingPlanner.Tests;

/// <summary>Checks for <see cref="RecipeDepths"/>.</summary>
public class RecipeDepthsTests
{
    /// <summary>A raw item is 0, and each crafted item is one more than its deepest ingredient.</summary>
    /// <param name="item">The item.</param>
    /// <param name="depth">Its expected depth.</param>
    [Theory]
    [InlineData("Log", 0)]
    [InlineData("Coal", 0)]
    [InlineData("Plank", 1)]
    [InlineData("Iron Ingot", 1)]
    [InlineData("Stick", 2)]
    [InlineData("Sword", 3)]
    [InlineData("Pickaxe", 3)]
    [InlineData("Torch", 3)]
    [InlineData("Tool Kit", 4)]
    [InlineData("Lantern", 4)]
    [InlineData("Camp Kit", 5)]
    public void Depth_IsOneMoreThanTheDeepestIngredient(string item, int depth)
    {
        var depths = RecipeDepths.Compute(Recipes);

        Assert.Equal(depth, depths[Id(item)]);
    }

    /// <summary>With no recipes there is nothing to report.</summary>
    [Fact]
    public void NoRecipes_GivesNoDepths()
    {
        var depths = RecipeDepths.Compute(new Dictionary<int, PlanRecipe>());

        Assert.Empty(depths);
    }

    /// <summary>A loop cannot be given a depth, and is reported rather than ignored.</summary>
    [Fact]
    public void LoopingRecipes_AreReported()
    {
        var loop = new Dictionary<int, PlanRecipe>
        {
            [1] = new(1, 0, [new PlanIngredient(2, 1)]),
            [2] = new(1, 0, [new PlanIngredient(1, 1)]),
        };

        Assert.Throws<InvalidOperationException>(() => RecipeDepths.Compute(loop));
    }
}
