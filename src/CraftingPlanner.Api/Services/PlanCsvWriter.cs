using System.Text;
using CraftingPlanner.Api.Contracts;

namespace CraftingPlanner.Api.Services;

/// <summary>
/// Writes a plan as a CSV file: one table with a Type column, so it opens cleanly in a
/// spreadsheet. Raw materials come first, then the steps in crafting order, then the total time.
/// </summary>
public static class PlanCsvWriter
{
    /// <summary>The first line of the file.</summary>
    public const string Header = "Type,Item,Quantity,Crafts,Made,Leftover,Seconds";

    /// <summary>Writes the plan as CSV text.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The file's contents, with Windows line endings so every spreadsheet program reads it.</returns>
    public static string Write(PlanResponse plan)
    {
        var text = new StringBuilder();
        text.Append(Header).Append("\r\n");

        foreach (var raw in plan.RawMaterials)
        {
            text.Append("Raw material,").Append(Field(raw.ItemName)).Append(',').Append(raw.Quantity).Append(",,,,\r\n");
        }

        foreach (var step in plan.Steps)
        {
            text.Append("Step,")
                .Append(Field(step.ItemName)).Append(',')
                .Append(step.Needed).Append(',')
                .Append(step.Crafts).Append(',')
                .Append(step.Made).Append(',')
                .Append(step.Leftover).Append(',')
                .Append(step.Seconds).Append("\r\n");
        }

        text.Append("Total time,,,,,,").Append(plan.TotalSeconds).Append("\r\n");

        return text.ToString();
    }

    /// <summary>
    /// Makes text safe as one CSV field. A field that contains a comma, a quote, or a line
    /// break is wrapped in quotes, and any quote inside it is doubled.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text as it should appear in the file.</returns>
    public static string Field(string text)
    {
        if (text.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
