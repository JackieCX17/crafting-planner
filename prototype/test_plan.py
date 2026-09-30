"""Checks for plan(). Run with:  py prototype/test_plan.py

Needs nothing beyond Python itself. Each check prints PASS or FAIL with the
expected and actual values. The cases come from section 5 of docs/DESIGN.md,
plus a few that catch specific mistakes.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from plan import Plan, Recipe, plan  # noqa: E402

# The recipes from the design document's worked example, plus three more
# (Torch, Lantern, Camp Kit) that use one item at two different depths.
RECIPES = {
    "Plank": Recipe(makes=2, seconds=2, ingredients={"Log": 1}),
    "Stick": Recipe(makes=4, seconds=2, ingredients={"Plank": 2}),
    "Iron Ingot": Recipe(makes=1, seconds=10, ingredients={"Iron Ore": 2, "Coal": 1}),
    "Sword": Recipe(makes=1, seconds=8, ingredients={"Iron Ingot": 2, "Stick": 1}),
    "Pickaxe": Recipe(makes=1, seconds=8, ingredients={"Iron Ingot": 3, "Stick": 2}),
    "Tool Kit": Recipe(makes=1, seconds=4, ingredients={"Sword": 1, "Pickaxe": 1}),
    "Torch": Recipe(makes=4, seconds=1, ingredients={"Coal": 1, "Stick": 1}),
    "Lantern": Recipe(makes=1, seconds=5, ingredients={"Torch": 1, "Iron Ingot": 1}),
    "Camp Kit": Recipe(makes=1, seconds=3, ingredients={"Lantern": 2, "Torch": 3}),
}

results = {"pass": 0, "fail": 0}


def check(label: str, actual, expected) -> None:
    """Compare and report one value."""
    ok = actual == expected
    results["pass" if ok else "fail"] += 1
    mark = "PASS" if ok else "FAIL"
    print(f"  {mark}  {label}")
    if not ok:
        print(f"         expected: {expected}")
        print(f"         actual:   {actual}")


def step_of(result: Plan, item: str):
    """The step for one item as (needed, crafts, made, leftover), or None."""
    for step in result.steps:
        if step.item == item:
            return (step.needed, step.crafts, step.made, step.leftover)
    return None


def check_order(result: Plan) -> None:
    """Every step must come after the steps of its crafted ingredients."""
    position = {step.item: index for index, step in enumerate(result.steps)}
    out_of_order = []
    for step in result.steps:
        for ingredient in RECIPES[step.item].ingredients:
            if ingredient in position and position[ingredient] > position[step.item]:
                out_of_order.append(f"{step.item} before {ingredient}")
    check("steps are in crafting order (ingredients first)", out_of_order, [])


def check_shape(result: Plan) -> None:
    """Steps hold only crafted items; raw holds only raw items; numbers agree."""
    check("steps contain only crafted items",
          [s.item for s in result.steps if s.item not in RECIPES], [])
    check("raw contains only raw items",
          [name for name in result.raw if name in RECIPES], [])
    check("each step lists each crafted item once",
          len({s.item for s in result.steps}), len(result.steps))
    check("made = crafts x makes, for every step",
          [s.item for s in result.steps if s.made != s.crafts * RECIPES[s.item].makes], [])
    check("leftover = made - needed, for every step",
          [s.item for s in result.steps if s.leftover != s.made - s.needed], [])
    check("total_seconds = sum of crafts x seconds",
          result.total_seconds,
          sum(s.crafts * RECIPES[s.item].seconds for s in result.steps))


def case_three_tool_kits() -> None:
    print("Case 1: 3 Tool Kits (the worked example in the design document)")
    result = plan("Tool Kit", 3, RECIPES)
    check("raw materials", result.raw, {"Iron Ore": 30, "Coal": 15, "Log": 3})
    check("Tool Kit step (needed, crafts, made, leftover)", step_of(result, "Tool Kit"), (3, 3, 3, 0))
    check("Sword step", step_of(result, "Sword"), (3, 3, 3, 0))
    check("Pickaxe step", step_of(result, "Pickaxe"), (3, 3, 3, 0))
    check("Iron Ingot step", step_of(result, "Iron Ingot"), (15, 15, 15, 0))
    check("Stick step", step_of(result, "Stick"), (9, 3, 12, 3))
    check("Plank step", step_of(result, "Plank"), (6, 3, 6, 0))
    check("total seconds", result.total_seconds, 222)
    check_order(result)
    check_shape(result)


def case_one_tool_kit_rounding() -> None:
    print("Case 2: 1 Tool Kit (total before rounding: 1 Log, not 2)")
    result = plan("Tool Kit", 1, RECIPES)
    check("raw materials", result.raw, {"Iron Ore": 10, "Coal": 5, "Log": 1})
    check("Stick step: 3 needed from two branches, one craft of four", step_of(result, "Stick"), (3, 1, 4, 1))
    check("Plank step", step_of(result, "Plank"), (2, 1, 2, 0))
    check_order(result)


def case_raw_target() -> None:
    print("Case 3: a raw item as the target")
    result = plan("Log", 5, RECIPES)
    check("raw materials", result.raw, {"Log": 5})
    check("no steps", result.steps, [])
    check("no time", result.total_seconds, 0)


def case_single_stick() -> None:
    print("Case 4: 1 Stick (a recipe that makes more than is needed)")
    result = plan("Stick", 1, RECIPES)
    check("Stick step", step_of(result, "Stick"), (1, 1, 4, 3))
    check("Plank step", step_of(result, "Plank"), (2, 1, 2, 0))
    check("raw materials", result.raw, {"Log": 1})


def case_two_depths() -> None:
    print("Case 5: 1 Camp Kit (Torch is needed directly and through Lantern)")
    # Processing Torch too early, before Lantern adds its share, gives 1 craft
    # instead of 2. This is the mistake a nearest-first order makes.
    result = plan("Camp Kit", 1, RECIPES)
    check("Lantern step", step_of(result, "Lantern"), (2, 2, 2, 0))
    check("Torch step: 3 direct + 2 via Lantern = 5, two crafts", step_of(result, "Torch"), (5, 2, 8, 3))
    check("Iron Ingot step", step_of(result, "Iron Ingot"), (2, 2, 2, 0))
    check("Stick step", step_of(result, "Stick"), (2, 1, 4, 2))
    check("Plank step", step_of(result, "Plank"), (2, 1, 2, 0))
    check("raw materials", result.raw, {"Coal": 4, "Iron Ore": 4, "Log": 1})
    check("total seconds", result.total_seconds, 39)
    check_order(result)
    check_shape(result)


def main() -> int:
    cases = [
        case_three_tool_kits,
        case_one_tool_kit_rounding,
        case_raw_target,
        case_single_stick,
        case_two_depths,
    ]
    for case in cases:
        try:
            case()
        except NotImplementedError as error:
            print(f"  not written yet: {error}")
            results["fail"] += 1
        except Exception as error:  # noqa: BLE001 - report any crash and keep going
            print(f"  CRASH  {type(error).__name__}: {error}")
            results["fail"] += 1
        print()

    print(f"{results['pass']} passed, {results['fail']} failed")
    return 0 if results["fail"] == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
