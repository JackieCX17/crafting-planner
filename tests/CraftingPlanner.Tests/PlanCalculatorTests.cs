using CraftingPlanner.Api.Services;
using static CraftingPlanner.Tests.SampleRecipes;

namespace CraftingPlanner.Tests;

/// <summary>
/// Checks for <see cref="PlanCalculator"/>. The cases and their expected numbers are the
/// same as in <c>prototype/test_plan.py</c>, so the C# and Python versions are held to
/// the same answers.
/// </summary>
public class PlanCalculatorTests
{
    /// <summary>Case 1: the worked example in the design document, raw materials.</summary>
    [Fact]
    public void ThreeToolKits_NeedsTheRawMaterialsFromTheDesignDocument()
    {
        var plan = Plan("Tool Kit", 3);

        Assert.Equal(
            [("Coal", 15L), ("Iron Ore", 30L), ("Log", 3L)],
            RawByName(plan));
    }

    /// <summary>Case 1: the worked example in the design document, one step at a time.</summary>
    /// <param name="item">The crafted item.</param>
    /// <param name="needed">Units required in total.</param>
    /// <param name="crafts">Times the recipe is run.</param>
    /// <param name="made">Units produced.</param>
    /// <param name="leftover">Units produced but not needed.</param>
    [Theory]
    [InlineData("Tool Kit", 3, 3, 3, 0)]
    [InlineData("Sword", 3, 3, 3, 0)]
    [InlineData("Pickaxe", 3, 3, 3, 0)]
    [InlineData("Iron Ingot", 15, 15, 15, 0)]
    [InlineData("Stick", 9, 3, 12, 3)]
    [InlineData("Plank", 6, 3, 6, 0)]
    public void ThreeToolKits_HasTheStepsFromTheDesignDocument(string item, long needed, long crafts, long made, long leftover)
    {
        var plan = Plan("Tool Kit", 3);

        Assert.Equal((needed, crafts, made, leftover), StepOf(plan, item));
    }

    /// <summary>Case 1: total time, and the time recorded on one step.</summary>
    [Fact]
    public void ThreeToolKits_TotalsTheTimeOfEveryCraft()
    {
        var plan = Plan("Tool Kit", 3);

        Assert.Equal(222, plan.TotalSeconds);
        Assert.Equal(6, plan.Steps.Single(step => step.ItemId == Id("Stick")).Seconds);
    }

    /// <summary>
    /// Case 2: demand is totalled before rounding. Sword and Pickaxe both need Sticks.
    /// Rounding each branch separately would craft Sticks twice and use 2 Logs; the
    /// right answer is one craft and 1 Log.
    /// </summary>
    [Fact]
    public void OneToolKit_TotalsDemandBeforeRounding()
    {
        var plan = Plan("Tool Kit", 1);

        Assert.Equal((3L, 1L, 4L, 1L), StepOf(plan, "Stick"));
        Assert.Equal((2L, 1L, 2L, 0L), StepOf(plan, "Plank"));
        Assert.Equal([("Coal", 5L), ("Iron Ore", 10L), ("Log", 1L)], RawByName(plan));
    }

    /// <summary>Case 3: a raw item as the target has no steps, only itself to gather.</summary>
    [Fact]
    public void RawTarget_IsJustItself()
    {
        var plan = Plan("Log", 5);

        Assert.Equal([("Log", 5L)], RawByName(plan));
        Assert.Empty(plan.Steps);
        Assert.Equal(0, plan.TotalSeconds);
    }

    /// <summary>Case 4: a recipe that makes more than is needed reports the leftover.</summary>
    [Fact]
    public void OneStick_ReportsTheLeftover()
    {
        var plan = Plan("Stick", 1);

        Assert.Equal((1L, 1L, 4L, 3L), StepOf(plan, "Stick"));
        Assert.Equal((2L, 1L, 2L, 0L), StepOf(plan, "Plank"));
        Assert.Equal([("Log", 1L)], RawByName(plan));
    }

    /// <summary>
    /// Case 5: an item needed at two depths. Camp Kit needs Torches directly and again
    /// through Lantern. Processing Torch before Lantern has added its share gives one craft
    /// instead of two.
    /// </summary>
    [Fact]
    public void OneCampKit_WaitsForEveryUserBeforeRounding()
    {
        var plan = Plan("Camp Kit", 1);

        Assert.Equal((2L, 2L, 2L, 0L), StepOf(plan, "Lantern"));
        Assert.Equal((5L, 2L, 8L, 3L), StepOf(plan, "Torch"));
        Assert.Equal((2L, 2L, 2L, 0L), StepOf(plan, "Iron Ingot"));
        Assert.Equal((2L, 1L, 4L, 2L), StepOf(plan, "Stick"));
        Assert.Equal((2L, 1L, 2L, 0L), StepOf(plan, "Plank"));
        Assert.Equal([("Coal", 4L), ("Iron Ore", 4L), ("Log", 1L)], RawByName(plan));
        Assert.Equal(39, plan.TotalSeconds);
    }

    /// <summary>Every step comes after the steps of its crafted ingredients.</summary>
    /// <param name="target">The item to make.</param>
    [Theory]
    [InlineData("Tool Kit")]
    [InlineData("Camp Kit")]
    public void Steps_AreInCraftingOrder(string target)
    {
        var plan = Plan(target, 3);

        var position = plan.Steps
            .Select((step, index) => (step.ItemId, index))
            .ToDictionary(pair => pair.ItemId, pair => pair.index);

        foreach (var step in plan.Steps)
        {
            foreach (var ingredient in Recipes[step.ItemId].Ingredients)
            {
                if (position.TryGetValue(ingredient.ItemId, out var ingredientPosition))
                {
                    Assert.True(
                        ingredientPosition < position[step.ItemId],
                        $"{Name(step.ItemId)} comes before its ingredient {Name(ingredient.ItemId)}.");
                }
            }
        }
    }

    /// <summary>Each crafted item appears once, and the numbers on every step agree with each other.</summary>
    [Fact]
    public void Steps_AreConsistent()
    {
        var plan = Plan("Camp Kit", 7);

        Assert.Equal(plan.Steps.Count, plan.Steps.Select(step => step.ItemId).Distinct().Count());
        Assert.All(plan.Steps, step =>
        {
            var recipe = Recipes[step.ItemId];
            Assert.Equal(step.Crafts * recipe.OutputQuantity, step.Made);
            Assert.Equal(step.Made - step.Needed, step.Leftover);
            Assert.Equal(step.Crafts * recipe.CraftSeconds, step.Seconds);
        });
        Assert.Equal(plan.Steps.Sum(step => step.Seconds), plan.TotalSeconds);
    }

    /// <summary>
    /// A chain of recipes that each need 1,000 of the next, asked for a million times,
    /// exceeds what a 64-bit number can hold within a few levels. That is refused rather
    /// than silently wrapping around to a wrong number.
    /// </summary>
    [Fact]
    public void HugeTotals_AreRefusedInsteadOfWrappingAround()
    {
        // Item 101 needs 1,000 of item 102, which needs 1,000 of item 103, and so on.
        var chain = Enumerable.Range(101, 6).ToDictionary(
            id => id,
            id => new PlanRecipe(1, 0, [new PlanIngredient(id + 1, 1000)]));

        var error = Assert.Throws<PlanTooLargeException>(() => PlanCalculator.Calculate(101, 1_000_000, chain));

        Assert.InRange(error.ItemId, 101, 107);
    }

    /// <summary>
    /// Loops are refused when a recipe is saved, so the calculation should never see one.
    /// If it does, it reports the problem instead of running forever or returning nonsense.
    /// </summary>
    [Fact]
    public void LoopingRecipes_AreReported()
    {
        var loop = new Dictionary<int, PlanRecipe>
        {
            [1] = new(1, 0, [new PlanIngredient(2, 1)]),
            [2] = new(1, 0, [new PlanIngredient(1, 1)]),
        };

        Assert.Throws<InvalidOperationException>(() => PlanCalculator.Calculate(1, 1, loop));
    }

    /// <summary>The raw materials of a plan as (name, quantity) pairs in name order.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The pairs.</returns>
    private static List<(string Name, long Quantity)> RawByName(PlanResult plan) =>
        plan.RawMaterials
            .Select(raw => (Name(raw.ItemId), raw.Quantity))
            .OrderBy(pair => pair.Item1)
            .ToList();

    /// <summary>The step for one item as (needed, crafts, made, leftover), or null when there is none.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="item">The crafted item's name.</param>
    /// <returns>The four numbers.</returns>
    private static (long Needed, long Crafts, long Made, long Leftover)? StepOf(PlanResult plan, string item)
    {
        var step = plan.Steps.SingleOrDefault(step => step.ItemId == Id(item));

        return step is null
            ? null
            : (step.Needed, step.Crafts, step.Made, step.Leftover);
    }
}
