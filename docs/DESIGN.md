# Crafting Planner: Design Document

| | |
|---|---|
| **Status** | Draft 1, written before any code |
| **Date** | 2026-09-28 |
| **Stack** | C# on .NET 10, ASP.NET Core Web API, SQLite, plain HTML and JavaScript |

## 1. Purpose

Crafting Planner answers one question: **"To make N of this item, what raw materials do I need?"**

Items are made from other items, which are themselves made from other items. Working out the total by hand is slow and error prone, especially when a recipe makes several units at a time or when two parts of a build share a component. The app stores items and recipes, then does that calculation on request.

The crafting theme is inspired by games such as Minecraft, where tools are built from layers of simpler items. The same problem appears in manufacturing, where it is called a bill of materials, and the logic is the same. The sample data uses generic item names and none of any game's artwork or branding.

## 2. Requirements

### Must have

| # | Requirement |
|---|---|
| M1 | Store items and recipes in a database |
| M2 | Create, read, update, and delete items and recipes through a web API |
| M3 | Calculate the raw materials, crafting steps, and leftovers for any item and quantity |
| M4 | Refuse invalid data with a clear reason, including recipes that would loop |
| M5 | A website that uses the API for everything it shows and changes |
| M6 | Documentation for every endpoint, class, function, and data field |
| M7 | Runs on a reviewer's machine with one command and no database setup |
| M8 | Automated tests for the calculation and the main endpoints |

### Nice to have

| # | Requirement |
|---|---|
| N1 | Stock on hand, so the plan can answer "what am I still missing?" |
| N2 | A craft action that uses up stock and adds the finished item |
| N3 | A hosted live demo |

### Deliberately left out

| Left out | Reason |
|---|---|
| Logins and user accounts | Adds a large amount of code that does not demonstrate the core problem |
| More than one recipe per item | Choosing between recipes is an optimization problem, which is a separate project |
| Byproducts (a recipe that makes two different things) | Complicates the calculation without adding a new idea |
| Fractional quantities | Whole numbers keep the rounding exact and avoid decimal errors |
| Several people editing at once | The app is a single-user demo |

## 3. Data model

```mermaid
erDiagram
    ITEM ||--o| RECIPE : "is produced by"
    RECIPE ||--|{ RECIPE_INGREDIENT : "lists"
    ITEM ||--o{ RECIPE_INGREDIENT : "is used in"
    ITEM {
        int Id PK
        string Name "unique"
        string Description
        string Category
    }
    RECIPE {
        int Id PK
        int OutputItemId FK "unique"
        int OutputQuantity
        int CraftSeconds
    }
    RECIPE_INGREDIENT {
        int RecipeId PK, FK
        int ItemId PK, FK
        int Quantity
    }
```

| Table | Field | Type | Rules |
|---|---|---|---|
| Item | Id | whole number | Set by the database |
| Item | Name | text | Required, unique, 1 to 80 characters |
| Item | Description | text | Optional, up to 500 characters |
| Item | Category | text | Optional, up to 40 characters, used for filtering |
| Recipe | Id | whole number | Set by the database |
| Recipe | OutputItemId | whole number | Required, must be an existing item, one recipe per item |
| Recipe | OutputQuantity | whole number | 1 to 1,000. How many units one craft makes |
| Recipe | CraftSeconds | whole number | 0 or more. Time for one craft |
| RecipeIngredient | RecipeId, ItemId | whole numbers | Together they identify the row, so an item appears once per recipe |
| RecipeIngredient | Quantity | whole number | 1 to 1,000. Units used by one craft |

**Raw or crafted is never stored.** An item is raw when it has no recipe. Working this out on request means the label can never disagree with the data.

## 4. Rules the backend enforces

| Rule | Response when broken |
|---|---|
| Required fields present, values within range | 400 Bad Request |
| Every ingredient refers to an existing item | 400 Bad Request |
| A recipe has at least one ingredient | 400 Bad Request |
| Item names are unique | 409 Conflict |
| An item has at most one recipe | 409 Conflict |
| A recipe cannot need its own output, directly or through other recipes | 409 Conflict |
| An item used as an ingredient cannot be deleted | 409 Conflict, listing the recipes that use it |
| The item or recipe in the address exists | 404 Not Found |
| Plan quantity is between 1 and 1,000,000 | 400 Bad Request |

Every rejection uses the same standard error format (Problem Details), with a short title, a plain explanation, and the field at fault where one applies.

### Decisions behind the rules

| Decision | Reason |
|---|---|
| Looping recipes are refused when saved, not when planning | The calculation can then trust the data, and the person who made the mistake is told immediately |
| Deleting an in-use item is blocked, not cascaded | Cascading would silently break every recipe that depends on it |
| Deleting an item also deletes its own recipe | A recipe has no meaning without the item it makes |
| 400 means the request itself is wrong; 409 means it clashes with existing data | Callers can tell "fix your input" apart from "the data has to change first" |

## 5. The planning calculation

### Steps

1. Collect every item the target depends on, at any depth.
2. Put them in an order where each item comes before its own ingredients.
3. Set the target's demand to the requested quantity.
4. Go through the items in order. For each crafted item:
   - crafts needed = demand divided by units per craft, rounded up
   - add (crafts needed x ingredient quantity) to each ingredient's demand
5. Items with no recipe are raw materials. Their demand is the final answer.

The ordering in step 2 guarantees that when an item is reached, everything that uses it has already been processed, so its demand is complete.

### Worked example

| Item | Made per craft | Ingredients per craft |
|---|---|---|
| Tool Kit | 1 | 1 Sword, 1 Pickaxe |
| Sword | 1 | 2 Iron Ingot, 1 Stick |
| Pickaxe | 1 | 3 Iron Ingot, 2 Stick |
| Iron Ingot | 1 | 2 Iron Ore, 1 Coal |
| Stick | 4 | 2 Plank |
| Plank | 2 | 1 Log |

**Plan for 3 Tool Kits:**

| Item | Needed | Crafts | Made | Left over |
|---|---|---|---|---|
| Tool Kit | 3 | 3 | 3 | 0 |
| Sword | 3 | 3 | 3 | 0 |
| Pickaxe | 3 | 3 | 3 | 0 |
| Iron Ingot | 15 | 15 | 15 | 0 |
| Stick | 9 | 3 | 12 | 3 |
| Plank | 6 | 3 | 6 | 0 |

**Raw materials: 30 Iron Ore, 15 Coal, 3 Log.**

### Key decision: total the demand before rounding

Sticks are needed by both the Sword and the Pickaxe. There are two ways to handle that, and they give different answers.

For 1 Tool Kit, which needs 3 Sticks in total (1 for the Sword, 2 for the Pickaxe):

| Approach | Stick crafts | Planks | Logs |
|---|---|---|---|
| Round each branch separately | 2 | 4 | 2 |
| **Total first, then round (chosen)** | 1 | 2 | 1 |

Rounding each branch separately makes 8 Sticks when 4 would do, and doubles the Logs. The chosen approach is why step 2 orders the items first: it is what makes a correct total possible.

### Limits

Totals use 64-bit whole numbers, and the plan quantity is capped at 1,000,000, so long chains of large recipes cannot overflow.

## 6. API

All addresses start with `/api`. Requests and responses use JSON.

### Items

| Method | Address | Purpose |
|---|---|---|
| GET | `/items` | List items. Filter by search text, category, or kind (raw or crafted). Sort and page |
| GET | `/items/{id}` | One item, with its recipe and the recipes that use it |
| POST | `/items` | Create an item |
| PUT | `/items/{id}` | Replace every field of an item |
| PATCH | `/items/{id}` | Change only the fields that are sent |
| DELETE | `/items/{id}` | Delete an item and its own recipe |

### Recipes

| Method | Address | Purpose |
|---|---|---|
| GET | `/recipes` | List recipes. Filter by an ingredient they use |
| GET | `/recipes/{id}` | One recipe with its ingredients |
| POST | `/recipes` | Create a recipe together with its ingredients |
| PUT | `/recipes/{id}` | Replace a recipe and its ingredients |
| DELETE | `/recipes/{id}` | Delete a recipe. Its item becomes raw |

### Ingredients, nested inside a recipe

| Method | Address | Purpose |
|---|---|---|
| GET | `/recipes/{id}/ingredients` | List a recipe's ingredients |
| PUT | `/recipes/{id}/ingredients/{itemId}` | Add an ingredient, or change its quantity |
| DELETE | `/recipes/{id}/ingredients/{itemId}` | Remove an ingredient |

### Planning and summaries

| Method | Address | Purpose |
|---|---|---|
| GET | `/items/{id}/plan?quantity=N` | Raw materials, crafting steps, leftovers, and total time |
| GET | `/items/{id}/plan/export?quantity=N` | The same plan as a downloadable shopping list (CSV) |
| GET | `/items/{id}/tree` | The item's recipe tree, for display |
| GET | `/stats` | Totals across the data: item counts, most-used ingredient, longest chain |

### Operation coverage

| Operation type | Where it appears |
|---|---|
| Create, read, update, delete | Items and recipes |
| Partial update | `PATCH /items/{id}` |
| Filter, search, sort, paging | `GET /items`, `GET /recipes` |
| Nested records | Ingredients inside recipes |
| Calculation | Plan and tree |
| Summary | Stats |
| Rejection | Every rule in section 4 |
| Export | Plan export |
| State-changing action | Craft (nice to have N2) |

## 7. Technology choices

| Choice | Reason |
|---|---|
| .NET 10 | Current long-term support release |
| ASP.NET Core with controllers | One class per resource, the most widely used structure in C# teams |
| Entity Framework Core | Standard C# database library. Tables are described as C# classes |
| SQLite | The database is one file. A reviewer installs nothing |
| Plain HTML and JavaScript, served by the same app | No build tools. Shows the API calls directly, with nothing hidden by a framework |
| Calculation kept separate from the database and the web layer | It can be tested on its own with plain inputs and outputs |
| Database tables are created at startup if missing | There is no deployed database to upgrade, so versioned schema changes (migrations) would add files without adding value. They are the first change to make before a real deployment |
| Plain HTTP on the local machine | Avoids certificate prompts for the reviewer. A hosted version would use HTTPS |
| xUnit for tests | Standard C# test library |

## 8. Documentation standard

| Layer | Standard |
|---|---|
| C# code | Every public class, function, and field has a structured comment. The build fails if one is missing |
| Interactive API page | Generated from those comments. Each endpoint shows inputs, an example, and its possible rejections |
| API guide | Copy-and-paste examples for each operation, plus the error format |
| Data dictionary | Every table and field with type, meaning, and rules |
| Front end code | Every JavaScript function has a structured comment |
| README | What the app is, how to run it, and a short tour |
| This document | Requirements, decisions, and reasons, updated when a decision changes |

## 9. Build stages

Each stage ends with something that runs, and with its documentation complete.

| Stage | Delivers |
|---|---|
| 1 | Project skeleton, database, Items API, interactive API page |
| 2 | Recipes and ingredients API, all rules from section 4 |
| 3 | Planning calculation: Python prototype first, then the C# version checked against it |
| 4 | Tree, stats, and export |
| 5 | Website |
| 6 | Tests, README, API guide, data dictionary, demo script |
| 7 | Optional: stock on hand, craft action, hosted demo |

## 10. Future work

- Stock on hand and the craft action, if not completed in stage 7
- Several recipes per item, with a choice of cheapest or fastest
- Byproducts
- Logins, so each person has their own items
- Undo for deletions
