/* The List page: the items on a saved list with their quantities, a picker to add more,
   and the plan for everything on it, redrawn after every change. */

const listId = Number(queryParam("id"));
const pageNotice = document.getElementById("page-notice");
const entriesNotice = document.getElementById("entries-notice");
const planNotice = document.getElementById("plan-notice");
const renameNotice = document.getElementById("rename-notice");
const renameForm = document.getElementById("rename-form");

/** The list as last loaded from the API. */
let list = null;

/** Every item by id, for the picker and for icons. */
const knownItems = new Map();

/** The picker used to add items. Built once. */
let picker = null;

/**
 * Loads the list and the items, then draws every part of the page.
 */
async function loadList() {
  clearNotice(pageNotice);

  try {
    const [loaded, items] = await Promise.all([api.get(`/api/lists/${listId}`), loadAllItems()]);
    list = loaded;
    for (const item of items) {
      knownItems.set(item.id, item);
    }
  } catch (error) {
    showProblem(pageNotice, error);
    return;
  }

  document.title = `${list.name} | Crafting Planner`;
  document.getElementById("title").textContent = list.name;
  document.getElementById("subtitle").textContent = list.description || "";
  document.getElementById("download").href = `/api/lists/${list.id}/plan/export`;
  document.getElementById("name").value = list.name;
  document.getElementById("description").value = list.description || "";

  if (picker === null) {
    picker = createPicker({
      items: [...knownItems.values()],
      buttonText: "Add",
      isHidden: (item) => list.entries.some((entry) => entry.itemId === item.id),
      onPick: (item) => setQuantity(item.id, 1, true),
    });
    document.getElementById("picker").replaceChildren(picker.element);
  } else {
    picker.refresh();
  }

  drawEntries();
  document.getElementById("content").classList.remove("hidden");
  await drawListPlan();
}

/**
 * Draws the items on the list, each with an editable quantity and a remove button.
 */
function drawEntries() {
  const panel = document.getElementById("entries");
  if (list.entries.length === 0) {
    panel.replaceChildren(el("p", { class: "muted" }, "Nothing on the list yet. Add items from the right."));
    return;
  }

  panel.replaceChildren(
    table(
      [{ text: "Item" }, { text: "Quantity", num: true }, { text: "" }],
      list.entries.map((entry) => [
        itemLink(entry.itemId, entry.itemName, entry),
        quantityInput(entry),
        removeButton(entry),
      ]),
    ),
  );
}

/**
 * Builds the quantity box for one entry. Saves when it loses focus or Enter is pressed.
 * @param {object} entry The entry.
 * @returns {HTMLElement} The input.
 */
function quantityInput(entry) {
  const input = el("input", {
    type: "number",
    min: 1,
    max: 1000000,
    value: entry.quantity,
    style: "width: 110px",
    "aria-label": `Quantity of ${entry.itemName}`,
  });
  const save = () => {
    const quantity = Number(input.value);
    if (quantity !== entry.quantity) {
      setQuantity(entry.itemId, quantity, false);
    }
  };
  input.addEventListener("change", save);
  input.addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      input.blur();
    }
  });
  return input;
}

/**
 * Builds the remove button for one entry.
 * @param {object} entry The entry.
 * @returns {HTMLElement} The button.
 */
function removeButton(entry) {
  const button = el("button", { class: "btn danger small", type: "button" }, "Remove");
  button.addEventListener("click", async () => {
    clearNotice(entriesNotice);
    try {
      await api.delete(`/api/lists/${listId}/items/${entry.itemId}`);
      await loadList();
    } catch (error) {
      showProblem(entriesNotice, error);
    }
  });
  return button;
}

/**
 * Puts an item on the list, or changes its quantity, then redraws the list and the plan.
 * @param {number} itemId The item's id.
 * @param {number} quantity How many units to make.
 * @param {boolean} focusQuantity Whether to put the cursor in the item's quantity box afterwards.
 */
async function setQuantity(itemId, quantity, focusQuantity) {
  clearNotice(entriesNotice);
  try {
    await api.put(`/api/lists/${listId}/items/${itemId}`, { quantity });
  } catch (error) {
    showProblem(entriesNotice, error);
    return;
  }

  await loadList();

  if (focusQuantity) {
    const item = knownItems.get(itemId);
    const box = document.querySelector(`#entries input[aria-label="Quantity of ${item ? item.name : ""}"]`);
    if (box) {
      box.focus();
      box.select();
    }
  }
}

/**
 * Asks the API for the plan for the whole list and draws it.
 */
async function drawListPlan() {
  clearNotice(planNotice);

  let plan;
  try {
    plan = await api.get(`/api/lists/${listId}/plan`);
  } catch (error) {
    showProblem(planNotice, error);
    return;
  }

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
}

/**
 * Saves a new name and note for the list.
 * @param {Event} event The form's submit event.
 */
async function rename(event) {
  event.preventDefault();
  clearNotice(renameNotice);
  try {
    await api.put(`/api/lists/${listId}`, {
      name: document.getElementById("name").value,
      description: document.getElementById("description").value,
    });
    renameForm.classList.add("hidden");
    await loadList();
  } catch (error) {
    showProblem(renameNotice, error);
  }
}

/**
 * Deletes the list after asking, then returns to the Lists page.
 */
async function deleteList() {
  if (!window.confirm(`Delete the list "${list.name}"? The items on it are not affected.`)) {
    return;
  }
  try {
    await api.delete(`/api/lists/${listId}`);
    window.location.href = "lists.html";
  } catch (error) {
    showProblem(pageNotice, error);
  }
}

document.getElementById("rename").addEventListener("click", () => {
  clearNotice(renameNotice);
  renameForm.classList.toggle("hidden");
  document.getElementById("name").focus();
});
document.getElementById("cancel-rename").addEventListener("click", () => renameForm.classList.add("hidden"));
renameForm.addEventListener("submit", rename);
document.getElementById("delete-list").addEventListener("click", deleteList);

if (Number.isInteger(listId) && listId > 0) {
  loadList();
} else {
  showProblem(pageNotice, new ApiError(400, { title: "No list chosen", detail: "Open a list from the Lists page." }));
}
