/* The Home page: a few numbers about the data, and the way in to each page. */

/**
 * Loads the summary and shows it as a row of figures.
 */
async function setUpHomePage() {
  const notice = document.getElementById("notice");
  const panel = document.getElementById("stats");

  let stats;
  try {
    stats = await api.get("/api/stats");
  } catch (error) {
    showProblem(notice, error);
    return;
  }

  const figures = [
    [formatNumber(stats.itemCount), "items"],
    [formatNumber(stats.craftedItemCount), "with a recipe"],
    [formatNumber(stats.rawItemCount), "raw materials"],
    [formatNumber(stats.categoryCount), "categories"],
  ];

  if (stats.mostUsedIngredient) {
    figures.push([stats.mostUsedIngredient.itemName, `most used ingredient, in ${stats.mostUsedIngredient.recipeCount} recipes`]);
  }

  if (stats.longestChain) {
    figures.push([stats.longestChain.itemName, `longest chain, ${stats.longestChain.depth} recipes deep`]);
  }

  panel.replaceChildren(
    ...figures.map(([value, label]) =>
      el("div", { class: "card stat" }, el("div", { class: "value" }, value), el("div", { class: "label" }, label)),
    ),
  );
}

setUpHomePage();
