/* The Plan page: pick an item and a quantity, show what it takes to make it. */

const planForm = document.getElementById("plan-form");
const quantityInput = document.getElementById("quantity");
const planNotice = document.getElementById("notice");
const resultPanel = document.getElementById("result");
const makePlanButton = document.getElementById("make-plan");

/** Every item by id, for icons in the plan tables. */
const knownItems = new Map();

/** The item picked to be planned, or null before one is picked. */
let chosenItem = null;

/**
 * Loads the items into the picker, then makes a plan straight away if the address names
 * an item, as links from the item page do.
 */
async function setUpPlanPage() {
  let items;
  try {
    items = await loadAllItems();
  } catch (error) {
    showProblem(planNotice, error);
    return;
  }

  for (const item of items) {
    knownItems.set(item.id, item);
  }

  const picker = createPicker({ items, buttonText: "Pick", onPick: choose });
  document.getElementById("picker").replaceChildren(picker.element);

  const quantity = queryParam("quantity");
  if (quantity) {
    quantityInput.value = quantity;
  }

  const wanted = knownItems.get(Number(queryParam("id")));
  if (wanted) {
    choose(wanted);
  }
}

/**
 * Sets the item to plan, shows it next to the quantity, and makes the plan.
 * @param {object} item The picked item.
 */
function choose(item) {
  chosenItem = item;
  document.getElementById("chosen").replaceChildren(itemIcon(item), item.name, " ", recipeTag(item.kind));
  document.getElementById("chosen").classList.remove("muted");
  makePlanButton.disabled = false;
  makePlan();
}

/**
 * Asks the API for the plan and draws it.
 */
async function makePlan() {
  if (chosenItem === null) {
    return;
  }
  clearNotice(planNotice);
  const id = chosenItem.id;
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

  document.getElementById("result-title").replaceChildren(
    itemIcon(chosenItem, true),
    `${formatNumber(plan.quantity)} × ${plan.itemName}`,
  );
  document.getElementById("download").href = `/api/items/${id}/plan/export?quantity=${encodeURIComponent(quantity)}`;
  document.getElementById("item-page").href = `item.html?id=${id}`;

  drawPlan(
    {
      raw: document.getElementById("raw"),
      steps: document.getElementById("steps"),
      time: document.getElementById("total-time"),
      count: document.getElementById("step-count"),
    },
    plan,
    knownItems,
  );

  resultPanel.classList.remove("hidden");
}

planForm.addEventListener("submit", (event) => {
  event.preventDefault();
  makePlan();
});

setUpPlanPage();
