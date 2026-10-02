/* Small helpers for building the page. Text always goes in through textContent,
   never through innerHTML, so an item name can never be mistaken for HTML. */

/**
 * Creates an element.
 * @param {string} tag The element's tag, such as "td".
 * @param {object} [attrs] Attributes to set. "class" and "for" work as written.
 * @param {...(Node|string)} children Text or elements to put inside.
 * @returns {HTMLElement} The element.
 */
function el(tag, attrs = {}, ...children) {
  const element = document.createElement(tag);
  for (const [name, value] of Object.entries(attrs)) {
    if (value === undefined || value === null || value === false) {
      continue;
    }
    element.setAttribute(name, value === true ? "" : value);
  }
  for (const child of children) {
    element.appendChild(typeof child === "string" ? document.createTextNode(child) : child);
  }
  return element;
}

/**
 * Formats a whole number with thousands separators.
 * @param {number} value The number.
 * @returns {string} For example "1,500,000".
 */
function formatNumber(value) {
  return Number(value).toLocaleString();
}

/**
 * Formats a number of seconds as hours, minutes, and seconds.
 * @param {number} seconds The duration.
 * @returns {string} For example "3m 42s" or "1h 5m 0s".
 */
function formatSeconds(seconds) {
  const total = Number(seconds);
  if (total < 60) {
    return `${total}s`;
  }
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const rest = total % 60;
  return hours > 0 ? `${hours}h ${minutes}m ${rest}s` : `${minutes}m ${rest}s`;
}

/**
 * Reads one value from the page's address, such as the "id" in "item.html?id=9".
 * @param {string} name The parameter's name.
 * @returns {string|null} The value, or null when it is absent.
 */
function queryParam(name) {
  return new URLSearchParams(window.location.search).get(name);
}

/** The icon used for an item that has none of its own, by category. */
const categoryIcons = {
  "raw material": "rock",
  component: "cardboard-box",
  tool: "hammer-nails",
  weapon: "two-handed-sword",
  utility: "anvil",
  kit: "backpack",
};

/**
 * Builds an item's icon: its own picture, or a faint one for its category when it has none.
 * @param {object} item Anything with "icon" and "category" fields, such as an item or a tree node.
 * @param {boolean} [large] Whether to draw it at heading size.
 * @returns {HTMLElement} The icon element.
 */
function itemIcon(item, large = false) {
  const own = item.icon;
  const fallback = categoryIcons[(item.category || "").toLowerCase()] || "cardboard-box";
  const file = `url(/icons/${own || fallback}.svg)`;
  return el("span", {
    class: `icon${large ? " large" : ""}${own ? "" : " faint"}`,
    style: `-webkit-mask-image: ${file}; mask-image: ${file}`,
    "aria-hidden": "true",
  });
}

/**
 * Builds a link to an item's own page, with its icon in front when the item is known.
 * @param {number} id The item's id.
 * @param {string} name The item's name, used as the link text.
 * @param {object} [item] The item, when its icon should be shown.
 * @returns {HTMLElement} The link.
 */
function itemLink(id, name, item = null) {
  const link = el("a", { href: `item.html?id=${id}` });
  if (item) {
    link.appendChild(itemIcon(item));
  }
  link.appendChild(document.createTextNode(name));
  return link;
}

/**
 * Builds the small "recipe" tag for an item that has a recipe, and nothing for one that
 * does not. The API calls these kinds "crafted" and "raw"; on the page, the plain fact
 * that there is a recipe reads better.
 * @param {string} kind "raw" or "crafted", as the API reports it.
 * @returns {Node} The tag, or an empty text node.
 */
function recipeTag(kind) {
  return kind === "crafted" ? el("span", { class: "tag recipe", title: "This item has a recipe" }, "recipe") : document.createTextNode("");
}

/**
 * The categories in use, in alphabetical order.
 * @param {Array<object>} items Every item.
 * @returns {Array<string>} The distinct category names.
 */
function categoriesOf(items) {
  return [...new Set(items.map((item) => item.category).filter(Boolean))].sort((a, b) => a.localeCompare(b));
}


/**
 * Builds a table.
 * @param {Array<{text: string, num?: boolean}>} columns The headings. "num" right-aligns the column.
 * @param {Array<Array<Node|string|number>>} rows The cells, one array per row.
 * @returns {HTMLElement} The table, wrapped so it can scroll on a narrow screen.
 */
function table(columns, rows) {
  const head = el("tr", {}, ...columns.map((column) => el("th", { class: column.num ? "num" : null }, column.text)));
  const body = rows.map((row) =>
    el(
      "tr",
      {},
      ...row.map((cell, index) =>
        el("td", { class: columns[index].num ? "num" : null }, typeof cell === "number" ? formatNumber(cell) : cell),
      ),
    ),
  );
  return el("div", { class: "table-wrap" }, el("table", {}, el("thead", {}, head), el("tbody", {}, ...body)));
}

/**
 * Loads every item, sorted by name, for select boxes. The API pages its lists, so
 * this asks for the largest page and follows on if there are more.
 * @returns {Promise<Array<object>>} The items.
 */
async function loadAllItems() {
  const items = [];
  let page = 1;
  let totalPages = 1;
  do {
    const result = await api.get(`/api/items?pageSize=100&page=${page}`);
    items.push(...result.items);
    totalPages = result.totalPages;
    page += 1;
  } while (page <= totalPages);
  return items;
}

/**
 * Marks the current page's link in the navigation.
 */
function markCurrentPage() {
  const here = window.location.pathname.split("/").pop() || "index.html";
  for (const link of document.querySelectorAll(".site-nav a")) {
    if (link.getAttribute("href") === here) {
      link.setAttribute("aria-current", "page");
    }
  }
}

document.addEventListener("DOMContentLoaded", markCurrentPage);
