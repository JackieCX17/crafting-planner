using CraftingPlanner.Api.Contracts;
using CraftingPlanner.Api.Services;

namespace CraftingPlanner.Tests;

/// <summary>Checks for <see cref="PlanCsvWriter"/>.</summary>
public class PlanCsvWriterTests
{
    /// <summary>The file has the header, one line per raw material and step, and a total line.</summary>
    [Fact]
    public void Write_ListsRawMaterialsThenStepsThenTheTotal()
    {
        var plan = new NamedPlan(
            [new RawMaterialLine { ItemId = 1, ItemName = "Log", Quantity = 1 }],
            [
                new PlanStepLine { ItemId = 4, ItemName = "Plank", Needed = 2, Crafts = 1, Made = 2, Leftover = 0, Seconds = 2 },
                new PlanStepLine { ItemId = 5, ItemName = "Stick", Needed = 1, Crafts = 1, Made = 4, Leftover = 3, Seconds = 2 },
            ],
            4);

        var lines = PlanCsvWriter.Write(plan).Split("\r\n");

        Assert.Equal(
            [
                "Type,Item,Quantity,Crafts,Made,Leftover,Seconds",
                "Raw material,Log,1,,,,",
                "Step,Plank,2,1,2,0,2",
                "Step,Stick,1,1,4,3,2",
                "Total time,,,,,,4",
                "",
            ],
            lines);
    }

    /// <summary>Names that would break a CSV file are quoted, and quotes inside them are doubled.</summary>
    /// <param name="name">The item name.</param>
    /// <param name="field">How it should appear in the file.</param>
    [Theory]
    [InlineData("Iron Ingot", "Iron Ingot")]
    [InlineData("Bow, long", "\"Bow, long\"")]
    [InlineData("Blade \"Dawn\"", "\"Blade \"\"Dawn\"\"\"")]
    [InlineData("Two\nlines", "\"Two\nlines\"")]
    public void Field_QuotesOnlyWhenNeeded(string name, string field)
    {
        Assert.Equal(field, PlanCsvWriter.Field(name));
    }
}
