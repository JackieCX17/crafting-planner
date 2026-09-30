/* The Plan page: choose an item and a quantity, show what it takes to make it. */

const planForm = document.getElementById("plan-form");
const itemSelect = document.getElementById("item");
const quantityInput = document.getElementById("quantity");
const planNotice = document.getElementById("notice");
const resultPanel = document.getElementById("result");

/**
 * Fills the item list, then makes a plan straight away if the address names an item,
 * as links from the item page do.
 */
async function setUpPlanPage() {
  try {
    fillItemSelect(itemSelect, await loadAllItems(), queryParam("id"));
  } catch (error) {
    showProblem(planNotice, error);
    return;
  }

  const quantity = queryParam("quantity");
  if (quantity) {
    quantityInput.value = quantity;
  }

  if (queryParam("id")) {
    await makePlan();
  }
}

/**
 * Asks the API for the plan and draws it.
 */
async function makePlan() {
  clearNotice(planNotice);
  const id = itemSelect.value;
  const quantity = quantityInput.value;

  let plan;
  try {
    plan = await api.get(`/api/items/${id}/plan?quantity=${encodeURIComponent(quantity)}`);
  } catch (error) {
    resultPanel.classList.add("hidden");
    showProblem(planNotice, error);
    return;
  }

  // Keep the address in step with the plan, so it can be bookmarked or shared.
  history.replaceState(null, "", `plan.html?id=${id}&quantity=${encodeURIComponent(quantity)}`);

  document.getElementById("result-title").textContent = `${formatNumber(plan.quantity)} × ${plan.itemName}`;
  document.getElementById("download").href = `/api/items/${id}/plan/export?quantity=${encodeURIComponent(quantity)}`;
  document.getElementById("item-page").href = `item.html?id=${id}`;
  document.getElementById("total-time").textContent = formatSeconds(plan.totalSeconds);
  document.getElementById("step-count").textContent =
    plan.steps.length === 0
      ? "Nothing to craft: this is a raw material."
      : `${plan.steps.length} step${plan.steps.length === 1 ? "" : "s"}, ${formatNumber(plan.steps.reduce((sum, step) => sum + step.crafts, 0))} crafts in all`;

  document.getElementById("raw").replaceChildren(
    table(
      [{ text: "Item" }, { text: "Quantity", num: true }],
      plan.rawMaterials.map((raw) => [itemLink(raw.itemId, raw.itemName), raw.quantity]),
    ),
  );

  document.getElementById("steps").replaceChildren(
    plan.steps.length === 0
      ? el("p", { class: "muted" }, "No crafting needed.")
      : table(
          [
            { text: "#" },
            { text: "Craft" },
            { text: "Needed", num: true },
            { text: "Crafts", num: true },
            { text: "Made", num: true },
            { text: "Left over", num: true },
            { text: "Time", num: true },
          ],
          plan.steps.map((step, index) => [
            String(index + 1),
            itemLink(step.itemId, step.itemName),
            step.needed,
            step.crafts,
            step.made,
            step.leftover,
            formatSeconds(step.seconds),
          ]),
        ),
  );

  resultPanel.classList.remove("hidden");
}

planForm.addEventListener("submit", (event) => {
  event.preventDefault();
  makePlan();
});

setUpPlanPage();
