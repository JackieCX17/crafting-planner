# Crafting Planner

Crafting Planner answers one question: **to make N of this item, what raw materials do I need?**

Items are made from other items, which are made from other items. The app stores items and recipes, and works out the totals. It is a C# web API with a website on top, built as a full stack exercise.

The crafting theme is inspired by games such as Minecraft. The same logic is used in manufacturing, where it is called a bill of materials.

![The Plan page: raw materials to gather and the crafting steps in order for three Tool Kits](docs/images/plan.png)

## Status

| Stage | Delivers | State |
|---|---|---|
| 1 | Project skeleton, database, Items API, interactive API page | Done |
| 2 | Recipes and ingredients API, all data rules | Done |
| 3 | Planning calculation, with automated tests | Done |
| 4 | Recipe tree, summaries, export | Done |
| 5 | Website | Done |
| 6 | Endpoint tests, data dictionary, walkthrough | Done |

All six planned stages are complete. The optional seventh (stock on hand, a craft action, a hosted demo) is listed under future work in the design document.

## Run it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Nothing else: the database is a single file that the app creates on first run, with sample data included.

```
dotnet run --project src/CraftingPlanner.Api
```

Then open <http://localhost:5080>.

| Address | What it is |
|---|---|
| <http://localhost:5080> | The website: plan an item, manage items and recipes |
| <http://localhost:5080/swagger> | The interactive API page: try every endpoint from the browser |

To start again with fresh sample data, stop the app and delete `src/CraftingPlanner.Api/craftingplanner.db`. The app asks for the same if the file was made by an older version with fewer columns.

## Run the tests

```
dotnet test
```

69 tests in two kinds:

- **Unit tests** hold the planning calculation to the worked example in the design document and to cases that catch specific mistakes, and cover the loop-free tree builder, chain depths, and the CSV export.
- **Endpoint tests** start the real app in memory on a fresh database and call it the way a client would: every kind of operation, every kind of rejection, the export download, the API description, and the website.

The same calculation cases are run against the Python prototype with `py prototype/test_plan.py`.

## The website

Plain HTML, CSS, and JavaScript, served by the same app. Every page reaches the backend only through the API, so the browser's network panel shows the same calls the interactive API page makes.

| Page | What it does |
|---|---|
| Home | Totals, the most used ingredient, the longest recipe chain, and the way in to each page |
| Plan | Pick an item by search or category, and a quantity. Get the raw materials, the crafting steps in order, the total time, and a CSV download |
| Items | Search, filter, sort, and page through items. Add and delete items |
| Item | Edit an item's details and picture, create or edit its recipe with a picker for ingredients, see what it is used in, and view its recipe tree |

Rejections from the API appear on the page in plain words. Try giving Planks a recipe that uses Sticks, and the page explains the loop.

Item pictures are by Lorc, Delapouite, and Faithtoken from [game-icons.net](https://game-icons.net), used under the [Creative Commons Attribution 3.0](https://creativecommons.org/licenses/by/3.0/) license. See `src/CraftingPlanner.Api/wwwroot/icons/LICENSE.md`.

![The Item page for the Sword: details, the recipe, what it is used in, and the recipe tree](docs/images/item.png)

## Try these on the API page

On the interactive API page, open an endpoint, choose **Try it out**, then **Execute**.

| Try | What it shows |
|---|---|
| `GET /api/items` with `kind` set to `crafted` | Filtering |
| `GET /api/items/9` | An item with its recipe and everything it is used in |
| `POST /api/items` with a new name | Insert |
| `PATCH /api/items/{id}` with only a category | Partial update |
| `POST /api/items` with the name `stick` | A rejection: the name is taken |
| `DELETE /api/items/9` | A rejection: other recipes depend on the item |
| `GET /api/recipes` with `uses` set to `9` | Every recipe that needs a Stick |
| `PUT /api/recipes/2/ingredients/1` with a quantity | Adding one ingredient to a recipe |
| `PUT /api/recipes/1/ingredients/9` with a quantity | A rejection: Planks made from Sticks would loop, because Sticks are made from Planks |
| `GET /api/items/18/plan` with `quantity` set to `3` | Everything it takes to make three Tool Kits, and the order to craft it in |
| `GET /api/items/18/plan/export` with `quantity` set to `3` | The same plan as a file that opens in a spreadsheet |
| `GET /api/items/11/tree` | The Sword's recipe drawn as a tree, down to the raw materials |
| `GET /api/stats` | Totals, the most used ingredient, and the longest recipe chain |

## Documentation

| Document | Contents |
|---|---|
| [Walkthrough](docs/WALKTHROUGH.md) | A five-minute route through everything the project demonstrates. Start here |
| [Design document](docs/DESIGN.md) | Requirements, data model, rules, and the reasons behind each decision |
| [API guide](docs/API-GUIDE.md) | Every operation with an example request and response |
| [Data dictionary](docs/DATA-DICTIONARY.md) | Every table and field, which rules live where, and the SQL that creates the tables |
| Interactive API page | Live reference at `/swagger`, generated from the comments in the code |

Every public class, function, and field in the C# code has a structured comment, and the build fails if one is missing. Every function in the website's JavaScript has one too.

## How it was built

1. The [design document](docs/DESIGN.md) was written first, before any code, and merged as the repository's first pull request.
2. Each stage in its build plan became one branch and one pull request, merged into `main` only when it ran and its documentation was complete.
3. The planning calculation was [prototyped in Python](prototype/README.md), then translated to C#, with tests holding both to the same answers.
4. Decisions made during the build were added to the design document with their reasons, including one correction to the original plan.

## Layout

```
docs/                        Design document, API guide, screenshots
prototype/                   The planning calculation in Python, written before the C# version
tests/CraftingPlanner.Tests/ Automated tests
src/CraftingPlanner.Api/
  Program.cs                 Startup: registers the parts and the request pipeline
  Controllers/               The endpoints
  Contracts/                 The shapes of requests and responses
  Services/                  Logic that needs no database or web: the loop search, the planning
                             calculation, the tree builder, chain depths, and the CSV writer
  Models/                    The database tables, described as classes
  Data/                      Database connection, table rules, sample data, shared queries
  wwwroot/                   The website: pages, stylesheet, and scripts
  CraftingPlanner.Api.http   Ready-made requests for editors that support them
```
