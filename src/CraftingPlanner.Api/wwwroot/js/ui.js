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

/**
 * Builds a link to an item's own page.
 * @param {number} id The item's id.
 * @param {string} name The item's name, used as the link text.
 * @returns {HTMLElement} The link.
 */
function itemLink(id, name) {
  return el("a", { href: `item.html?id=${id}` }, name);
}

/**
 * Builds the raw or crafted tag.
 * @param {string} kind "raw" or "crafted".
 * @returns {HTMLElement} The tag.
 */
function kindTag(kind) {
  return el("span", { class: `tag ${kind}` }, kind);
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
 * Fills a select box with items.
 * @param {HTMLSelectElement} select The select box.
 * @param {Array<object>} items The items to list.
 * @param {number|string|null} [selectedId] The id to select, if any.
 */
function fillItemSelect(select, items, selectedId = null) {
  select.replaceChildren(...items.map((item) => el("option", { value: item.id }, item.name)));
  if (selectedId !== null) {
    select.value = String(selectedId);
  }
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
