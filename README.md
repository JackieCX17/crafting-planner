# Crafting Planner

Crafting Planner answers one question: **to make N of this item, what raw materials do I need?**

Items are made from other items, which are made from other items. The app stores items and recipes, and works out the totals. It is a C# web API with a website on top, built as a full stack exercise.

The crafting theme is inspired by games such as Minecraft. The same logic is used in manufacturing, where it is called a bill of materials.

## Status

| Stage | Delivers | State |
|---|---|---|
| 1 | Project skeleton, database, Items API, interactive API page | Done |
| 2 | Recipes and ingredients API, all data rules | Done |
| 3 | Planning calculation | Next |
| 4 | Recipe tree, summaries, export | Planned |
| 5 | Website | Planned |
| 6 | Tests, full documentation, demo script | Planned |

## Run it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). Nothing else: the database is a single file that the app creates on first run, with sample data included.

```
dotnet run --project src/CraftingPlanner.Api
```

Then open <http://localhost:5080>. It shows the interactive API page, where every endpoint can be tried from the browser.

To start again with fresh sample data, stop the app and delete `src/CraftingPlanner.Api/craftingplanner.db`.

## Try these

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

## Documentation

| Document | Contents |
|---|---|
| [Design document](docs/DESIGN.md) | Requirements, data model, rules, and the reasons behind each decision |
| [API guide](docs/API-GUIDE.md) | Every operation with an example request and response |
| Interactive API page | Live reference, generated from the comments in the code |

Every public class, function, and field in the code has a structured comment. The build fails if one is missing.

## Layout

```
docs/                        Design document and API guide
src/CraftingPlanner.Api/
  Program.cs                 Startup: registers the parts and the request pipeline
  Controllers/               The endpoints
  Contracts/                 The shapes of requests and responses
  Services/                  Logic that needs no database or web, such as the loop search
  Models/                    The database tables, described as classes
  Data/                      Database connection, table rules, sample data
  CraftingPlanner.Api.http   Ready-made requests for editors that support them
```
