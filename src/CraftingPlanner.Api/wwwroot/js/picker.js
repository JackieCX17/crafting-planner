/* The item picker: a search box, a row of category chips, and a list of matching items
   with a button on each. Used wherever a page needs the person to choose an item. */

/** The largest number of matches the picker shows at once. */
const PICKER_LIMIT = 30;

/**
 * Builds an item picker.
 * @param {object} options
 * @param {Array<object>} options.items Every item that can be picked.
 * @param {function(object): void} options.onPick Called with the item when its button is pressed.
 * @param {string} [options.buttonText] The text on each item's button. Default "Add".
 * @param {function(object): boolean} [options.isDisabled] Returns true for an item whose button should be greyed out.
 * @returns {{element: HTMLElement, refresh: function(Array<object>=): void}} The picker and a way to redraw it.
 */
function createPicker({ items, onPick, buttonText = "Add", isDisabled = () => false }) {
  let all = items;
  let chosenCategory = null;

  const search = el("input", { type: "search", placeholder: "Search by name or description", "aria-label": "Search items" });
  const chips = el("div", { class: "chips" });
  const results = el("div", { class: "picker-results" });
  const element = el("div", { class: "picker" }, search, chips, results);

  /** The categories present, in alphabetical order, with "Other" for items that have none. */
  function categories() {
    const names = new Set(all.map((item) => item.category || "Other"));
    return [...names].sort((a, b) => a.localeCompare(b));
  }

  /** Redraws the chip row, marking the chosen one. */
  function drawChips() {
    const chip = (label, value) => {
      const button = el("button", { type: "button", class: `chip${chosenCategory === value ? " on" : ""}` }, label);
      button.addEventListener("click", () => {
        chosenCategory = value;
        drawChips();
        drawResults();
      });
      return button;
    };
    chips.replaceChildren(chip("All", null), ...categories().map((name) => chip(name, name)));
  }

  /** Redraws the list of matches for the current search text and chip. */
  function drawResults() {
    const text = search.value.trim().toLowerCase();
    const matches = all.filter((item) => {
      const inCategory = chosenCategory === null || (item.category || "Other") === chosenCategory;
      const inText =
        text === "" ||
        item.name.toLowerCase().includes(text) ||
        (item.description || "").toLowerCase().includes(text);
      return inCategory && inText;
    });

    const shown = matches.slice(0, PICKER_LIMIT);
    results.replaceChildren(
      ...shown.map((item) => {
        const button = el("button", { type: "button", class: "btn small", disabled: isDisabled(item) }, buttonText);
        button.addEventListener("click", () => onPick(item));
        return el(
          "div",
          { class: "picker-row" },
          el("span", { class: "picker-name" }, itemIcon(item), item.name, " ", kindTag(item.kind)),
          el("span", { class: "muted picker-category" }, item.category || ""),
          button,
        );
      }),
      matches.length === 0
        ? el("p", { class: "muted" }, "No items match.")
        : matches.length > shown.length
          ? el("p", { class: "muted" }, `Showing ${shown.length} of ${matches.length}. Narrow the search to see the rest.`)
          : "",
    );
  }

  /**
   * Redraws the picker, with a new set of items when one is given.
   * @param {Array<object>} [newItems] The items to pick from.
   */
  function refresh(newItems) {
    if (newItems) {
      all = newItems;
    }
    drawChips();
    drawResults();
  }

  search.addEventListener("input", drawResults);
  refresh();

  return { element, refresh };
}
