/* The Item page: details, the recipe editor, what the item is used in, and its recipe tree. */

const itemId = Number(queryParam("id"));
const pageNotice = document.getElementById("page-notice");
const detailsNotice = document.getElementById("details-notice");
const recipeNotice = document.getElementById("recipe-notice");
const recipeForm = document.getElementById("recipe-form");
const ingredientRows = document.getElementById("ingredients");

/** The item as last loaded from the API. */
let item = null;

/** Every item, for the ingredient select boxes. Loaded once. */
let allItems = [];

/**
 * Loads the item and draws every part of the page.
 */
async function loadItem() {
  clearNotice(pageNotice);

  try {
    [item, allItems] = await Promise.all([api.get(`/api/items/${itemId}`), loadAllItems()]);
  } catch (error) {
    showProblem(pageNotice, error);
    return;
  }

  document.title = `${item.name} | Crafting Planner`;
  document.getElementById("title").textContent = item.name;
  document.getElementById("subtitle").replaceChildren(
    kindTag(item.kind),
    item.category ? ` · ${item.category}` : "",
  );
  document.getElementById("plan-link").href = `plan.html?id=${item.id}`;

  document.getElementById("name").value = item.name;
  document.getElementById("category").value = item.category || "";
  document.getElementById("description").value = item.description || "";

  drawUsedIn();
  drawRecipe();
  await drawTree();

  document.getElementById("content").classList.remove("hidden");
}

/**
 * Draws the list of recipes that use this item as an ingredient.
 */
function drawUsedIn() {
  const panel = document.getElementById("used-in");
  if (item.usedIn.length === 0) {
    panel.replaceChildren(el("p", { class: "muted" }, "Not used in any recipe."));
    return;
  }
  panel.replaceChildren(
    table(
      [{ text: "Item" }, { text: "Per craft", num: true }],
      item.usedIn.map((use) => [itemLink(use.itemId, use.itemName), use.quantity]),
    ),
  );
}

/**
 * Draws the recipe summary and the buttons for it. The editor stays hidden until asked for.
 */
function drawRecipe() {
  const summary = document.getElementById("recipe-summary");
  const actions = document.getElementById("recipe-actions");
  clearNotice(recipeNotice);
  recipeForm.classList.add("hidden");

  if (item.recipe === null) {
    summary.textContent = "This item is raw: it is gathered, not crafted.";
    const add = el("button", { class: "btn small", type: "button" }, "Add a recipe");
    add.addEventListener("click", () => openEditor());
    actions.replaceChildren(add);
    return;
  }

  const parts = item.recipe.ingredients.map((line) => `${line.quantity} × ${line.itemName}`).join(", ");
  summary.textContent =
    `One craft makes ${item.recipe.outputQuantity} and takes ${formatSeconds(item.recipe.craftSeconds)}. ` +
    `It uses ${parts}.`;

  const edit = el("button", { class: "btn small", type: "button" }, "Edit recipe");
  edit.addEventListener("click", () => openEditor());

  const remove = el("button", { class: "btn danger small", type: "button" }, "Delete recipe");
  remove.addEventListener("click", deleteRecipe);

  actions.replaceChildren(edit, remove);
}

/**
 * Shows the recipe editor, filled from the current recipe or empty for a new one.
 */
function openEditor() {
  clearNotice(recipeNotice);
  document.getElementById("output-quantity").value = item.recipe ? item.recipe.outputQuantity : 1;
  document.getElementById("craft-seconds").value = item.recipe ? item.recipe.craftSeconds : 0;

  ingredientRows.replaceChildren();
  if (item.recipe) {
    for (const line of item.recipe.ingredients) {
      addIngredientRow(line.itemId, line.quantity);
    }
  } else {
    addIngredientRow();
  }

  recipeForm.classList.remove("hidden");
}

/**
 * Adds one ingredient row to the editor: an item to pick, a quantity, and a remove button.
 * @param {number|null} [selectedId] The item to select, or null for the first item.
 * @param {number} [quantity] The quantity to show.
 */
function addIngredientRow(selectedId = null, quantity = 1) {
  const select = el("select", { required: true });
  // An item cannot be its own ingredient, so it is left out of the list.
  fillItemSelect(select, allItems.filter((candidate) => candidate.id !== item.id), selectedId);

  const input = el("input", { type: "number", min: 1, max: 1000, value: quantity, required: true });
  const remove = el("button", { class: "btn danger small", type: "button" }, "Remove");
  const row = el("div", { class: "ingredient-row" }, select, input, remove);
  remove.addEventListener("click", () => row.remove());
  ingredientRows.appendChild(row);
}

/**
 * Reads the editor into the shape the API expects.
 * @returns {object} The recipe request.
 */
function readEditor() {
  const ingredients = [...ingredientRows.querySelectorAll(".ingredient-row")].map((row) => ({
    itemId: Number(row.querySelector("select").value),
    quantity: Number(row.querySelector("input").value),
  }));

  return {
    outputQuantity: Number(document.getElementById("output-quantity").value),
    craftSeconds: Number(document.getElementById("craft-seconds").value),
    ingredients,
  };
}

/**
 * Saves the recipe: creates it when the item is raw, replaces it otherwise.
 * @param {Event} event The form's submit event.
 */
async function saveRecipe(event) {
  event.preventDefault();
  clearNotice(recipeNotice);
  const request = readEditor();

  try {
    if (item.recipe === null) {
      await api.post("/api/recipes", { outputItemId: item.id, ...request });
    } else {
      await api.put(`/api/recipes/${item.recipe.id}`, request);
    }
  } catch (error) {
    showProblem(recipeNotice, error);
    return;
  }

  await loadItem();
  showSuccess(recipeNotice, "The recipe was saved.");
}

/**
 * Deletes the recipe after asking. The item is kept and becomes raw.
 */
async function deleteRecipe() {
  if (!window.confirm(`Delete the recipe for "${item.name}"? The item is kept and becomes raw.`)) {
    return;
  }
  try {
    await api.delete(`/api/recipes/${item.recipe.id}`);
    await loadItem();
    showSuccess(recipeNotice, "The recipe was deleted. The item is now raw.");
  } catch (error) {
    showProblem(recipeNotice, error);
  }
}

/**
 * Saves the item's details.
 * @param {Event} event The form's submit event.
 */
async function saveDetails(event) {
  event.preventDefault();
  clearNotice(detailsNotice);

  const request = {
    name: document.getElementById("name").value,
    category: document.getElementById("category").value,
    description: document.getElementById("description").value,
  };

  try {
    await api.put(`/api/items/${item.id}`, request);
    await loadItem();
    showSuccess(detailsNotice, "The details were saved.");
  } catch (error) {
    showProblem(detailsNotice, error);
  }
}

/**
 * Deletes the item after asking, then returns to the list. The API refuses when
 * other recipes still use the item, and the rejection lists them.
 */
async function deleteItem() {
  if (!window.confirm(`Delete "${item.name}"? Its own recipe is deleted with it.`)) {
    return;
  }
  try {
    await api.delete(`/api/items/${item.id}`);
    window.location.href = "items.html";
  } catch (error) {
    showProblem(detailsNotice, error);
  }
}

/**
 * Loads the recipe tree and draws it as nested lists. Hidden for a raw item.
 */
async function drawTree() {
  const card = document.getElementById("tree-card");
  if (item.recipe === null) {
    card.classList.add("hidden");
    return;
  }
  card.classList.remove("hidden");

  let tree;
  try {
    tree = await api.get(`/api/items/${item.id}/tree`);
  } catch (error) {
    showProblem(pageNotice, error);
    return;
  }

  document.getElementById("tree").replaceChildren(el("ul", { class: "tree" }, treeBranch(tree, true)));
}

/**
 * Draws one node of the tree and everything under it.
 * @param {object} node The node from the API.
 * @param {boolean} isRoot Whether this is the item at the top, which has no quantity of its own.
 * @returns {HTMLElement} The list item.
 */
function treeBranch(node, isRoot) {
  const line = el("li", {});
  if (!isRoot) {
    line.appendChild(el("span", { class: "qty" }, `${formatNumber(node.quantity)} ×`));
  }
  line.appendChild(isRoot ? el("strong", {}, node.itemName) : itemLink(node.itemId, node.itemName));
  line.appendChild(document.createTextNode(" "));
  line.appendChild(kindTag(node.kind));

  if (node.kind === "crafted") {
    line.appendChild(el("span", { class: "note" }, ` makes ${node.outputQuantity} per craft`));
  }

  if (node.truncated) {
    line.appendChild(el("span", { class: "note" }, " (too deep to show here; open the item to see its tree)"));
  } else if (node.ingredients.length > 0) {
    line.appendChild(el("ul", {}, ...node.ingredients.map((child) => treeBranch(child, false))));
  }

  return line;
}

document.getElementById("details-form").addEventListener("submit", saveDetails);
document.getElementById("delete-item").addEventListener("click", deleteItem);
recipeForm.addEventListener("submit", saveRecipe);
document.getElementById("add-ingredient").addEventListener("click", () => addIngredientRow());
document.getElementById("cancel-recipe").addEventListener("click", () => drawRecipe());

if (Number.isInteger(itemId) && itemId > 0) {
  loadItem();
} else {
  showProblem(pageNotice, new ApiError(400, { title: "No item chosen", detail: "Open an item from the Items page." }));
}
