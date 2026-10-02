/* Draws a plan into a page: the raw materials, the crafting steps, and the total time.
   Shared by the Plan page and the List page, so a plan looks the same everywhere. */

/**
 * Draws a plan.
 * @param {object} into The elements to draw into: raw, steps, time, and count.
 * @param {object} plan The plan from the API: rawMaterials, steps, and totalSeconds.
 * @param {Map<number, object>} knownItems Every item by id, for icons.
 */
function drawPlan(into, plan, knownItems) {
  into.time.textContent = formatSeconds(plan.totalSeconds);
  into.count.textContent =
    plan.steps.length === 0
      ? "Nothing to craft."
      : `${plan.steps.length} step${plan.steps.length === 1 ? "" : "s"}, ${formatNumber(plan.steps.reduce((sum, step) => sum + step.crafts, 0))} batches in all`;

  into.raw.replaceChildren(
    plan.rawMaterials.length === 0
      ? el("p", { class: "muted" }, "Nothing to gather.")
      : table(
          [{ text: "Item" }, { text: "Quantity", num: true }],
          plan.rawMaterials.map((raw) => [itemLink(raw.itemId, raw.itemName, knownItems.get(raw.itemId)), raw.quantity]),
        ),
  );

  if (plan.steps.length === 0) {
    into.steps.replaceChildren(el("p", { class: "muted" }, "No crafting needed."));
    return;
  }

  const steps = table(
    [
      { text: "#" },
      { text: "Craft" },
      { text: "Required #", num: true },
      { text: "Batches", num: true },
      { text: "Produced #", num: true },
      { text: "Left over", num: true },
      { text: "Time", num: true },
    ],
    plan.steps.map((step, index) => [
      String(index + 1),
      itemLink(step.itemId, step.itemName, knownItems.get(step.itemId)),
      step.needed,
      step.crafts,
      step.made,
      step.leftover,
      formatSeconds(step.seconds),
    ]),
  );

  // A last row with the totals that add up: crafts and time.
  steps.querySelector("tbody").appendChild(
    el(
      "tr",
      { class: "total" },
      el("td", {}),
      el("td", {}, "Total"),
      el("td", {}),
      el("td", { class: "num" }, formatNumber(plan.steps.reduce((sum, step) => sum + step.crafts, 0))),
      el("td", {}),
      el("td", {}),
      el("td", { class: "num" }, formatSeconds(plan.totalSeconds)),
    ),
  );

  into.steps.replaceChildren(steps);
}
