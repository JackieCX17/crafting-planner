/* The Items page: a filterable, paged list, a form to add an item, and delete buttons. */

const listNotice = document.getElementById("list-notice");
const addNotice = document.getElementById("add-notice");
const pageSize = 20;
let currentPage = 1;

/**
 * Builds the address of the list request from the filter form and the current page.
 * @returns {string} The address, such as "/api/items?kind=crafted&page=2&pageSize=20".
 */
function listAddress() {
  const params = new URLSearchParams({ page: currentPage, pageSize });
  const search = document.getElementById("search").value.trim();
  const category = document.getElementById("category").value.trim();
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
        [{ text: "Name" }, { text: "Category" }, { text: "Kind" }, { text: "Description" }, { text: "" }],
        result.items.map((item) => [
          itemLink(item.id, item.name, item),
          item.category || "",
          kindTag(item.kind),
          item.description || "",
          deleteButton(item),
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
 * Builds the delete button for one row. Deleting asks first, and the API refuses
 * when other recipes still use the item.
 * @param {object} item The item.
 * @returns {HTMLElement} The button.
 */
function deleteButton(item) {
  const button = el("button", { class: "btn danger small", type: "button" }, "Delete");
  button.addEventListener("click", async () => {
    if (!window.confirm(`Delete "${item.name}"? Its own recipe is deleted with it.`)) {
      return;
    }
    try {
      await api.delete(`/api/items/${item.id}`);
      showSuccess(listNotice, `"${item.name}" was deleted.`);
      await loadItems();
    } catch (error) {
      showProblem(listNotice, error);
    }
  });
  return button;
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

document.getElementById("filter-form").addEventListener("submit", (event) => {
  event.preventDefault();
  currentPage = 1;
  loadItems();
});

document.getElementById("prev").addEventListener("click", () => {
  currentPage -= 1;
  loadItems();
});

document.getElementById("next").addEventListener("click", () => {
  currentPage += 1;
  loadItems();
});

loadItems();
