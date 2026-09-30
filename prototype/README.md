# Prototype of the planning calculation

The planning calculation is the heart of the app, so it was written in Python first,
where it could be worked out and checked quickly, and then translated to C#.

| File | Purpose |
|---|---|
| `plan.py` | The calculation, with the data shapes it works on |
| `test_plan.py` | The checks: the worked example from the design document, plus cases that catch specific mistakes |

Run the checks from the repository root:

```
py prototype/test_plan.py
```

The C# version is `src/CraftingPlanner.Api/Services/PlanCalculator.cs`. The tests in `tests/CraftingPlanner.Tests/PlanCalculatorTests.cs` hold it to the same cases and the same numbers.
