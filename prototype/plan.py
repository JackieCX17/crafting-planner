"""Prototype of the planning calculation, written in Python before the C# version.

The question it answers: to make `quantity` of `target`, what raw materials are
needed, how many times is each recipe crafted, and what is left over?

Section 5 of docs/DESIGN.md describes the steps and works through an example.
test_plan.py holds that example and a few more as checks. Run them with:

    py prototype/test_plan.py

Item names are used as keys here, to keep the prototype readable. The C# version
uses item ids, and is checked against the same cases.
"""

from dataclasses import dataclass


@dataclass
class Recipe:
    """How one item is made."""

    makes: int
    """How many units one craft produces."""

    seconds: int
    """How long one craft takes."""

    ingredients: dict[str, int]
    """Item name to the units one craft uses up."""


@dataclass
class Step:
    """One crafted item in the plan."""

    item: str
    needed: int
    """Units required in total, by the target and by other steps."""

    crafts: int
    """How many times the recipe is run: needed divided by makes, rounded up."""

    made: int
    """Units produced: crafts times makes."""

    leftover: int
    """Units produced but not needed: made minus needed."""


@dataclass
class Plan:
    """The answer."""

    raw: dict[str, int]
    """Raw item name to the total units to gather."""

    steps: list[Step]
    """The crafted items, in the order to craft them: an item comes after every
    crafted ingredient it needs, so its ingredients are ready when its turn comes."""

    total_seconds: int
    """Sum over the steps of crafts times the recipe's seconds."""


def plan(target: str, quantity: int, recipes: dict[str, Recipe]) -> Plan:
    """Work out the plan for `quantity` units of `target`.

    `recipes` maps each crafted item's name to its recipe. An item that is not in
    `recipes` is raw. The recipes contain no loops; the API refuses those when
    they are saved.

    The steps from the design document:

    1. Collect every item the target depends on, at any depth.
    2. Put them in an order where each item comes before its own ingredients.
    3. Set the target's demand to `quantity`.
    4. Go through the items in that order. For each crafted item:
       - crafts = demand divided by makes, rounded up
       - add crafts times the ingredient quantity to each ingredient's demand
    5. Items with no recipe are raw. Their demand is the final answer.

    Step 2 is what makes the totals right. When an item's turn comes, everything
    that uses it has already been processed, so its demand is complete before it
    is rounded. See "Key decision" in section 5 of the design document.

    Note that the order in step 2 is the reverse of the order the plan reports
    its steps in.
    """
    raise NotImplementedError("plan() has not been written yet")
