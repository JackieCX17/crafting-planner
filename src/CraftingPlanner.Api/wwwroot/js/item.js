/* The Item page: the item form filled in, what the item is used in, which lists it is on,
   and its recipe tree. */

const itemId = Number(queryParam("id"));
const pageNotice = document.getElementById("page-notice");

/** The item as last loaded from the API. */
let item = null;

/** Every item, for icons and the ingredient picker. Loaded once. */
let allItems = [];

/** The pictures an item can be given. Loaded once. */
let icons = null;

/**
 * Loads the item and draws every part of the page.
 * @param {string} [message] A confirmation to show, such as after a save.
 */
async function loadItem(message) {
  clearNotice(pageNotice);

  try {
    [item, allItems, icons] = await Promise.all([
      api.get(`/api/items/${itemId}`),
      loadAllItems(),
      icons ?? api.get("/api/icons"),
    ]);
  } catch (error) {
    showProblem(pageNotice, error);
    return;
  }

  document.title = `${item.name} | Crafting Planner`;
  document.getElementById("title").replaceChildren(itemIcon(item, true), item.name);
  document.getElementById("subtitle").replaceChildren(item.category || "No category", " ", recipeTag(item.kind));
  document.getElementById("plan-link").href = `plan.html?id=${item.id}`;

  createItemForm({
    container: document.getElementById("form"),
    allItems,
    icons,
    existing: item,
    onSaved: () => loadItem("Saved."),
    onCancel: () => loadItem(),
  });

  drawUsedIn();
  drawOnLists();
  await drawTree();

  document.getElementById("content").classList.remove("hidden");
  if (message) {
    showSuccess(pageNotice, message);
  }
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
    showProblem(pageNotice, error);
  }
}

/**
 * Loads the recipe tree and draws it as nested lists. Hidden for an item with no recipe.
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

document.getElementById("delete-item").addEventListener("click", deleteItem);

if (Number.isInteger(itemId) && itemId > 0) {
  loadItem();
} else {
  showProblem(pageNotice, new ApiError(400, { title: "No item chosen", detail: "Open an item from the Items page." }));
}
