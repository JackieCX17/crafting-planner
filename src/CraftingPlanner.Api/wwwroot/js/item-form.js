/* The item form: name, category, description, picture, and an optional recipe with its
   ingredients, all saved with one button. The New item page and the Item page both use it,
   so adding an item and changing one look and work the same way. */

/**
 * Builds the item form inside a container.
 * @param {object} options
 * @param {HTMLElement} options.container Where the form goes.
 * @param {Array<object>} options.allItems Every item, for the ingredient picker and category suggestions.
 * @param {Array<object>} options.icons The pictures an item can be given, from the API.
 * @param {object|null} options.existing The item being changed, with its recipe, or null for a new item.
 * @param {function(number): void} options.onSaved Called with the item's id after a successful save.
 * @param {function(): void} options.onCancel Called when the Cancel button is pressed.
 */
function createItemForm({ container, allItems, icons, existing, onSaved, onCancel }) {
  const notice = el("div", { class: "notice" });

  // Details. The category is chosen from the ones in use; the last choice reveals a box
  // for a category that does not exist yet.
  const NEW_CATEGORY = "__new__";
  const name = el("input", { id: "name", required: true, maxlength: 80, placeholder: "Copper Ore" });
  const category = el("select", { id: "category" });
  const newCategory = el("input", { id: "new-category", class: "hidden", maxlength: 40, placeholder: "Type the new category", style: "margin-top: 6px" });
  category.replaceChildren(
    el("option", { value: "" }, "No category"),
    ...categoriesOf(allItems).map((entry) => el("option", { value: entry }, entry)),
    el("option", { value: NEW_CATEGORY }, "New category…"),
  );
  const description = el("textarea", { id: "description", maxlength: 500, rows: 2, placeholder: "What it is." });
  const iconPreview = el("span");
  const icon = el("select", { id: "icon", style: "flex: 1" });
  icon.replaceChildren(
    el("option", { value: "" }, "None (use the category's)"),
    ...icons.map((entry) => el("option", { value: entry.name }, `${entry.name} (by ${entry.author})`)),
  );

  // Recipe
  const hasRecipe = el("input", { type: "checkbox", id: "has-recipe" });
  const outputQuantity = el("input", { id: "output-quantity", type: "number", min: 1, max: 1000, value: 1, required: true });
  const craftSeconds = el("input", { id: "craft-seconds", type: "number", min: 0, max: 86400, value: 0, required: true });
  const rows = el("div", { id: "ingredients" });
  const emptyNote = el("p", { class: "muted" }, "No ingredients yet. Pick some below.");
  const pickerHolder = el("div");
  const recipeSection = el(
    "div",
    { id: "recipe-section", class: "recipe-section hidden" },
    el("div", { class: "row", style: "display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 12px" },
      el("label", {}, "Makes per craft", outputQuantity),
      el("label", {}, "Seconds per craft", craftSeconds)),
    el("div", { class: "muted", style: "font-size: 14px; margin-bottom: 6px" }, "Ingredients, per craft"),
    rows,
    emptyNote,
    el("div", { class: "muted", style: "font-size: 14px; margin: 12px 0 6px" }, "Add an ingredient"),
    pickerHolder,
  );

  const saveButton = el("button", { class: "btn", type: "submit" }, existing ? "Save" : "Create item");
  const cancelButton = el("button", { class: "btn secondary", type: "button" }, existing ? "Discard changes" : "Cancel");
  const form = el(
    "form",
    { id: "item-form" },
    el("div", { class: "grid" },
      el("div", {},
        el("label", {}, "Name", name),
        el("label", {}, "Category", category, newCategory),
        el("label", {}, "Description", description),
        el("label", {}, "Picture", el("span", { style: "display: flex; gap: 8px; align-items: center" }, iconPreview, icon))),
      el("div", {},
        el("label", { class: "checkbox" }, hasRecipe, " This item has a recipe"),
        el("p", { class: "muted", style: "font-size: 14px" }, "Leave it unticked for a material that is gathered, not made."),
        recipeSection)),
    el("div", { class: "actions", style: "margin-top: 16px" }, saveButton, cancelButton),
    notice,
  );

  // An item cannot be its own ingredient, and an ingredient already in the recipe is
  // listed above, so both are left out of the picker.
  const picker = createPicker({
    items: allItems.filter((candidate) => !existing || candidate.id !== existing.id),
    buttonText: "Add",
    isHidden: (candidate) => rowFor(candidate.id) !== null,
    onPick: (candidate) => {
      addIngredientRow(candidate.id, 1);
      picker.refresh();
      rowFor(candidate.id).querySelector("input").focus();
    },
  });
  pickerHolder.replaceChildren(picker.element);

  /**
   * Finds the ingredient row for an item.
   * @param {number} id The item's id.
   * @returns {HTMLElement|null} The row, or null when the item is not an ingredient.
   */
  function rowFor(id) {
    return rows.querySelector(`.ingredient-row[data-item-id="${id}"]`);
  }

  /** Shows or hides the "no ingredients yet" note. */
  function updateEmptyNote() {
    emptyNote.classList.toggle("hidden", rows.children.length > 0);
  }

  /**
   * Adds one ingredient row: the item's icon and name, a quantity, and a remove button.
   * @param {number} id The ingredient item's id.
   * @param {number} quantity The quantity to show.
   */
  function addIngredientRow(id, quantity) {
    const ingredient = allItems.find((candidate) => candidate.id === id) || { id, name: `Item ${id}`, category: null, icon: null };
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
      picker.refresh();
    });
    rows.appendChild(row);
    updateEmptyNote();
  }

  /**
   * The category as it will be saved: the chosen one, or the typed one when "New category" is chosen.
   * @returns {string} The category, or an empty string for none.
   */
  function chosenCategory() {
    return category.value === NEW_CATEGORY ? newCategory.value : category.value;
  }

  /** Shows the box for a new category only when "New category" is chosen. */
  function toggleNewCategory() {
    const typing = category.value === NEW_CATEGORY;
    newCategory.classList.toggle("hidden", !typing);
    newCategory.required = typing;
    if (typing) {
      newCategory.focus();
    }
  }

  /** Shows the picture chosen in the select box. */
  function previewIcon() {
    iconPreview.replaceChildren(itemIcon({ icon: icon.value || null, category: chosenCategory() }, true));
  }

  /** Shows or hides the recipe fields to match the checkbox. */
  function toggleRecipe() {
    recipeSection.classList.toggle("hidden", !hasRecipe.checked);
    outputQuantity.required = hasRecipe.checked;
    craftSeconds.required = hasRecipe.checked;
  }

  /**
   * Fills the form from an item.
   * @param {object} item The item, with its recipe.
   */
  function fill(item) {
    name.value = item.name;
    if (item.category && ![...category.options].some((option) => option.value === item.category)) {
      category.insertBefore(el("option", { value: item.category }, item.category), category.lastElementChild);
    }
    category.value = item.category || "";
    newCategory.value = "";
    toggleNewCategory();
    description.value = item.description || "";
    icon.value = item.icon || "";
    hasRecipe.checked = item.recipe !== null;
    outputQuantity.value = item.recipe ? item.recipe.outputQuantity : 1;
    craftSeconds.value = item.recipe ? item.recipe.craftSeconds : 0;
    rows.replaceChildren();
    for (const line of item.recipe ? item.recipe.ingredients : []) {
      addIngredientRow(line.itemId, line.quantity);
    }
    updateEmptyNote();
    toggleRecipe();
    previewIcon();
    picker.refresh();
  }

  /**
   * Reads the form into the shapes the API expects.
   * @returns {{item: object, recipe: object|null}} The item fields, and the recipe or null for none.
   */
  function read() {
    const recipe = hasRecipe.checked
      ? {
          outputQuantity: Number(outputQuantity.value),
          craftSeconds: Number(craftSeconds.value),
          ingredients: [...rows.querySelectorAll(".ingredient-row")].map((row) => ({
            itemId: Number(row.dataset.itemId),
            quantity: Number(row.querySelector("input").value),
          })),
        }
      : null;

    return {
      item: { name: name.value, category: chosenCategory(), description: description.value, icon: icon.value },
      recipe,
    };
  }

  /**
   * Saves the item and then its recipe. A new item is created first; if its recipe is then
   * refused, the item stays and the form switches to editing it, so a second Save fixes the
   * recipe without creating the item twice.
   * @param {Event} event The form's submit event.
   */
  async function save(event) {
    event.preventDefault();
    clearNotice(notice);
    const data = read();

    if (data.recipe && data.recipe.ingredients.length === 0) {
      showProblem(notice, new ApiError(400, {
        title: "The recipe needs an ingredient",
        detail: "Add at least one ingredient, or untick \"This item has a recipe\".",
      }));
      return;
    }

    if (!data.recipe && existing?.recipe && !window.confirm(`Remove the recipe from "${existing.name}"? It becomes a gathered material.`)) {
      return;
    }

    let itemId;
    try {
      if (existing) {
        await api.put(`/api/items/${existing.id}`, data.item);
        itemId = existing.id;
      } else {
        existing = await api.post("/api/items", data.item);
        itemId = existing.id;
        saveButton.textContent = "Save";
      }
    } catch (error) {
      showProblem(notice, error);
      return;
    }

    try {
      if (data.recipe && existing.recipe) {
        await api.put(`/api/recipes/${existing.recipe.id}`, data.recipe);
      } else if (data.recipe) {
        await api.post("/api/recipes", { outputItemId: itemId, ...data.recipe });
      } else if (existing.recipe) {
        await api.delete(`/api/recipes/${existing.recipe.id}`);
      }
    } catch (error) {
      showProblem(notice, error);
      notice.prepend(el("div", {}, "The item was saved, but its recipe was not. Fix the recipe and save again."));
      return;
    }

    onSaved(itemId);
  }

  hasRecipe.addEventListener("change", toggleRecipe);
  icon.addEventListener("change", previewIcon);
  category.addEventListener("change", () => {
    toggleNewCategory();
    previewIcon();
  });
  newCategory.addEventListener("input", previewIcon);
  cancelButton.addEventListener("click", onCancel);
  form.addEventListener("submit", save);

  if (existing) {
    fill(existing);
  } else {
    updateEmptyNote();
    toggleRecipe();
    toggleNewCategory();
    previewIcon();
  }

  container.replaceChildren(form);
}
