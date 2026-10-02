/* The Items page: a filterable, paged list and a form to add an item. Editing and
   deleting happen on the item's own page, where what it is used in can be seen. */

const listNotice = document.getElementById("list-notice");
const addNotice = document.getElementById("add-notice");
const pageSize = 20;
let currentPage = 1;

/**
 * Fills the category dropdown and the category suggestions from the items that exist.
 * They are worked out in the browser from the full item list, which suits hundreds of
 * items; at thousands, the API would be asked for the distinct categories instead.
 */
async function setUpCategories() {
  let items;
  try {
    items = await loadAllItems();
  } catch {
    return; // The list below will report the problem.
  }

  const select = document.getElementById("category");
  const chosen = select.value;
  select.replaceChildren(
    el("option", { value: "" }, "Any"),
    ...categoriesOf(items).map((name) => el("option", { value: name }, name)),
  );
  select.value = chosen;

  fillCategoryDatalist(document.getElementById("categories"), items);
}

/**
 * Builds the address of the list request from the filter form and the current page.
 * @returns {string} The address, such as "/api/items?kind=crafted&page=2&pageSize=20".
 */
function listAddress() {
  const params = new URLSearchParams({ page: currentPage, pageSize });
  const search = document.getElementById("search").value.trim();
  const category = document.getElementById("category").value;
  const kind = document.getElementById("kind").value;
  const sort = document.getElementById("sort").value;

  if (search) params.set("search", search);
  if (category) params.set("category", category);
  if (kind) params.set("kind", kind);
  if (sort === "id") {
    params.set("sort", "id");
    params.set("order", "desc");
  } else {
    params.set("sort", sort);
  }

  return `/api/items?${params}`;
}

/**
 * Loads one page of items and draws the table.
 */
async function loadItems() {
  clearNotice(listNotice);

  let result;
  try {
    result = await api.get(listAddress());
  } catch (error) {
    showProblem(listNotice, error);
    return;
  }

  const list = document.getElementById("list");
  if (result.items.length === 0) {
    list.replaceChildren(el("p", { class: "muted" }, "No items match."));
  } else {
    list.replaceChildren(
      table(
        [{ text: "Name" }, { text: "Category" }, { text: "Description" }],
        result.items.map((item) => [
          el("span", {}, itemLink(item.id, item.name, item), " ", recipeTag(item.kind)),
          item.category || "",
          item.description || "",
        ]),
      ),
    );
  }

  const first = (result.page - 1) * result.pageSize + 1;
  const last = first + result.items.length - 1;
  document.getElementById("page-info").textContent =
    result.totalCount === 0 ? "" : `Showing ${first} to ${last} of ${formatNumber(result.totalCount)}`;
  document.getElementById("prev").disabled = result.page <= 1;
  document.getElementById("next").disabled = result.page >= result.totalPages;
}

/**
 * Creates an item from the add form, then opens its page.
 * @param {Event} event The form's submit event.
 */
async function addItem(event) {
  event.preventDefault();
  clearNotice(addNotice);

  const request = {
    name: document.getElementById("add-name").value,
    category: document.getElementById("add-category").value,
    description: document.getElementById("add-description").value,
  };

  try {
    const item = await api.post("/api/items", request);
    window.location.href = `item.html?id=${item.id}`;
  } catch (error) {
    showProblem(addNotice, error);
  }
}

document.getElementById("add-form").addEventListener("submit", addItem);

/** The timer that waits for typing to pause before searching. */
let searchTimer = null;

/**
 * Reloads the list from the first page, after a short pause when called from typing,
 * so the API is not asked on every keystroke.
 * @param {number} delay How long to wait, in milliseconds. 0 reloads at once.
 */
function reloadFromFirstPage(delay) {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    currentPage = 1;
    loadItems();
  }, delay);
}

document.getElementById("search").addEventListener("input", () => reloadFromFirstPage(250));
for (const id of ["category", "kind", "sort"]) {
  document.getElementById(id).addEventListener("change", () => reloadFromFirstPage(0));
}

// Enter in the search box searches at once instead of reloading the page.
document.getElementById("filter-form").addEventListener("submit", (event) => {
  event.preventDefault();
  reloadFromFirstPage(0);
});

document.getElementById("prev").addEventListener("click", () => {
  currentPage -= 1;
  loadItems();
});

document.getElementById("next").addEventListener("click", () => {
  currentPage += 1;
  loadItems();
});

setUpCategories();
loadItems();
