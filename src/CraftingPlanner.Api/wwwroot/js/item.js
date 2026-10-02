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
  document.getElementById("title").replaceChildren(itemIcon(item, true), item.name);
  document.getElementById("subtitle").replaceChildren(
    item.category || "No category",
    " ",
    recipeTag(item.kind),
  );
  fillCategoryDatalist(document.getElementById("categories"), allItems);
  document.getElementById("plan-link").href = `plan.html?id=${item.id}`;

  document.getElementById("name").value = item.name;
  document.getElementById("category").value = item.category || "";
  document.getElementById("description").value = item.description || "";
  await fillIconSelect(item.icon);

  drawUsedIn();
  drawOnLists();
  drawRecipe();
  await drawTree();

  document.getElementById("content").classList.remove("hidden");

  // item.html?id=5&edit=recipe opens the recipe editor straight away.
  if (queryParam("edit") === "recipe") {
    openEditor();
    history.replaceState(null, "", `item.html?id=${item.id}`);
  }
}

/** The icons the website ships with, loaded once. */
let iconCatalog = null;

/**
 * Fills the icon select box from the API's list of icons, with a "none" choice first,
 * and shows a preview of the chosen one next to it.
 * @param {string|null} selected The item's current icon name.
 */
async function fillIconSelect(selected) {
  const select = document.getElementById("icon");
  iconCatalog ??= await api.get("/api/icons");

  select.replaceChildren(
    el("option", { value: "" }, "None (use the category's)"),
    ...iconCatalog.map((icon) => el("option", { value: icon.name }, `${icon.name} (by ${icon.author})`)),
  );
  select.value = selected || "";
  previewIcon();
}

/**
 * Shows the icon chosen in the select box, so a choice can be seen before it is saved.
 */
function previewIcon() {
  const chosen = document.getElementById("icon").value;
  document.getElementById("icon-preview").replaceChildren(
    itemIcon({ icon: chosen || null, category: document.getElementById("category").value }, true),
  );
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
      item.usedIn.map((use) => [itemLink(use.itemId, use.itemName, lookupItem(use.itemId)), use.quantity]),
    ),
  );
}

/**
 * Draws the shopping lists this item is on.
 */
function drawOnLists() {
  const panel = document.getElementById("on-lists");
  if (item.onLists.length === 0) {
    panel.replaceChildren(el("p", { class: "muted" }, "Not on any list."));
    return;
  }
  panel.replaceChildren(
    table(
      [{ text: "List" }, { text: "Quantity", num: true }],
      item.onLists.map((use) => [el("a", { href: `list.html?id=${use.listId}` }, use.listName), use.quantity]),
    ),
  );
}

/**
 * Finds an item in the list of all items, for its icon and category.
 * @param {number} id The item's id.
 * @returns {object|null} The item, or null when it is not in the list.
 */
function lookupItem(id) {
  return allItems.find((candidate) => candidate.id === id) || null;
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

/** The picker used to add ingredients. Built once the editor first opens. */
let ingredientPicker = null;

/**
 * Shows the recipe editor, filled from the current recipe or empty for a new one.
 */
function openEditor() {
  clearNotice(recipeNotice);
  document.getElementById("output-quantity").value = item.recipe ? item.recipe.outputQuantity : 1;
  document.getElementById("craft-seconds").value = item.recipe ? item.recipe.craftSeconds : 0;

  ingredientRows.replaceChildren();
  for (const line of item.recipe ? item.recipe.ingredients : []) {
    addIngredientRow(line.itemId, line.quantity);
  }

  // An item cannot be its own ingredient, so it is left out of the picker.
  const candidates = allItems.filter((candidate) => candidate.id !== item.id);
  if (ingredientPicker === null) {
    ingredientPicker = createPicker({
      items: candidates,
      buttonText: "Add",
      isDisabled: (candidate) => rowFor(candidate.id) !== null,
      onPick: (candidate) => {
        addIngredientRow(candidate.id, 1);
        ingredientPicker.refresh();
        rowFor(candidate.id).querySelector("input").focus();
      },
    });
    document.getElementById("ingredient-picker").replaceChildren(ingredientPicker.element);
  } else {
    ingredientPicker.refresh(candidates);
  }

  updateEmptyNote();
  recipeForm.classList.remove("hidden");
}

/**
 * Finds the editor row for an item.
 * @param {number} id The item's id.
 * @returns {HTMLElement|null} The row, or null when the item is not in the recipe.
 */
function rowFor(id) {
  return ingredientRows.querySelector(`.ingredient-row[data-item-id="${id}"]`);
}

/**
 * Shows or hides the "no ingredients yet" note.
 */
function updateEmptyNote() {
  document.getElementById("no-ingredients").classList.toggle("hidden", ingredientRows.children.length > 0);
}

/**
 * Adds one ingredient row to the editor: the item's icon and name, a quantity, and a remove button.
 * @param {number} id The ingredient item's id.
 * @param {number} quantity The quantity to show.
 */
function addIngredientRow(id, quantity) {
  const ingredient = lookupItem(id) || { id, name: `Item ${id}`, category: null, icon: null };
  const input = el("input", { type: "number", min: 1, max: 1000, value: quantity, required: true, "aria-label": `Quantity of ${ingredient.name}` });
  const remove = el("button", { class: "btn danger small", type: "button" }, "Remove");
  const row = el(
    "div",
    { class: "ingredient-row", "data-item-id": id },
    el("span", { class: "ingredient-name" }, itemIcon(ingredient), ingredient.name),
    input,
    remove,
  );
  remove.addEventListener("click", () => {
    row.remove();
    updateEmptyNote();
    ingredientPicker?.refresh();
  });
  ingredientRows.appendChild(row);
  updateEmptyNote();
}

/**
 * Reads the editor into the shape the API expects.
 * @returns {object} The recipe request.
 */
function readEditor() {
  const ingredients = [...ingredientRows.querySelectorAll(".ingredient-row")].map((row) => ({
    itemId: Number(row.dataset.itemId),
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
    icon: document.getElementById("icon").value,
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
  const listNote = item.onLists.length > 0 ? ` It is also taken off ${item.onLists.length} list${item.onLists.length === 1 ? "" : "s"}.` : "";
  if (!window.confirm(`Delete "${item.name}"? Its own recipe is deleted with it.${listNote}`)) {
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
  const known = lookupItem(node.itemId);
  line.appendChild(isRoot ? el("strong", {}, known ? itemIcon(known) : "", node.itemName) : itemLink(node.itemId, node.itemName, known));
  if (node.kind === "crafted") {
    line.appendChild(el("span", { class: "note" }, ` makes ${node.outputQuantity} per craft`));
  } else {
    line.appendChild(el("span", { class: "note" }, " gathered"));
  }

  if (node.truncated) {
    line.appendChild(el("span", { class: "note" }, " (too deep to show here; open the item to see its tree)"));
  } else if (node.ingredients.length > 0) {
    line.appendChild(el("ul", {}, ...node.ingredients.map((child) => treeBranch(child, false))));
  }

  return line;
}

document.getElementById("details-form").addEventListener("submit", saveDetails);
document.getElementById("icon").addEventListener("change", previewIcon);
document.getElementById("category").addEventListener("input", previewIcon);
document.getElementById("delete-item").addEventListener("click", deleteItem);
recipeForm.addEventListener("submit", saveRecipe);
document.getElementById("cancel-recipe").addEventListener("click", () => drawRecipe());

if (Number.isInteger(itemId) && itemId > 0) {
  loadItem();
} else {
  showProblem(pageNotice, new ApiError(400, { title: "No item chosen", detail: "Open an item from the Items page." }));
}
