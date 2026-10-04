/* The New item page: the item form, empty. Saving opens the new item's page. */

/**
 * Loads what the form needs and shows it.
 */
async function setUpNewItemPage() {
  const notice = document.getElementById("page-notice");

  let allItems;
  let icons;
  try {
    [allItems, icons] = await Promise.all([loadAllItems(), api.get("/api/icons")]);
  } catch (error) {
    showProblem(notice, error);
    return;
  }

  createItemForm({
    container: document.getElementById("form"),
    allItems,
    icons,
    existing: null,
    onSaved: (itemId) => {
      window.location.href = `item.html?id=${itemId}`;
    },
    onCancel: () => {
      window.location.href = "items.html";
    },
  });

  document.getElementById("name").focus();
}

setUpNewItemPage();
