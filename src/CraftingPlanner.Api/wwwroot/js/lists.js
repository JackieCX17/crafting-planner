/* The Lists page: every saved list, a form to create one, and delete buttons. */

const listsNotice = document.getElementById("list-notice");
const addListNotice = document.getElementById("add-notice");

/**
 * Loads the lists and draws the table.
 */
async function loadLists() {
  clearNotice(listsNotice);

  let result;
  try {
    result = await api.get("/api/lists?pageSize=100");
  } catch (error) {
    showProblem(listsNotice, error);
    return;
  }

  const panel = document.getElementById("lists");
  if (result.items.length === 0) {
    panel.replaceChildren(el("p", { class: "muted" }, "No lists yet. Create one above."));
    return;
  }

  panel.replaceChildren(
    table(
      [{ text: "Name" }, { text: "Note" }, { text: "Items", num: true }, { text: "" }],
      result.items.map((list) => [
        el("a", { href: `list.html?id=${list.id}` }, list.name),
        list.description || "",
        list.entryCount,
        el("span", { class: "actions" }, el("a", { class: "btn secondary small", href: `list.html?id=${list.id}` }, "Open"), deleteListButton(list)),
      ]),
    ),
  );
}

/**
 * Builds the delete button for one list. Deleting asks first. The items on the list are not affected.
 * @param {object} list The list.
 * @returns {HTMLElement} The button.
 */
function deleteListButton(list) {
  const button = el("button", { class: "btn danger small", type: "button" }, "Delete");
  button.addEventListener("click", async () => {
    if (!window.confirm(`Delete the list "${list.name}"? The items on it are not affected.`)) {
      return;
    }
    try {
      await api.delete(`/api/lists/${list.id}`);
      showSuccess(listsNotice, `"${list.name}" was deleted.`);
      await loadLists();
    } catch (error) {
      showProblem(listsNotice, error);
    }
  });
  return button;
}

/**
 * Creates a list from the form, then opens it.
 * @param {Event} event The form's submit event.
 */
async function addList(event) {
  event.preventDefault();
  clearNotice(addListNotice);

  try {
    const list = await api.post("/api/lists", {
      name: document.getElementById("add-name").value,
      description: document.getElementById("add-description").value,
    });
    window.location.href = `list.html?id=${list.id}`;
  } catch (error) {
    showProblem(addListNotice, error);
  }
}

document.getElementById("add-form").addEventListener("submit", addList);
loadLists();
