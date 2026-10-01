# Crafting Planner API Guide

This guide shows every operation with an example request and the response it returns. The interactive API page at <http://localhost:5080/swagger> is the live reference. This guide is the walkthrough.

**Covered:** every endpoint in the API.

| Section | Contents |
|---|---|
| [Items](#items) | Things that can be crafted or used as ingredients |
| [Recipes](#recipes) | What goes into an item, and how many come out |
| [Ingredients](#ingredients) | Changing one line of a recipe |
| [Planning](#planning) | What it takes to make a quantity of an item, as JSON or as a file, and the recipe tree |
| [Summary](#summary) | Totals and highlights across all the data |
| [Icons](#icons) | The pictures an item can be given |
| [Rejections](#rejections) | The error format, with an example of each kind |

## Basics

| | |
|---|---|
| Base address | `http://localhost:5080` |
| Format | JSON in, JSON out |
| Field names | camelCase, such as `pageSize` |
| Login | None |

### Ways to send a request

| Tool | How |
|---|---|
| Interactive API page | Open an endpoint, choose **Try it out**, then **Execute** |
| `.http` file | Open `src/CraftingPlanner.Api/CraftingPlanner.Api.http` in Visual Studio, VS Code, or Rider |
| curl | `curl -X POST http://localhost:5080/api/items -H "Content-Type: application/json" -d '{"name":"Copper Ore"}'` |
| PowerShell | `Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/items -ContentType "application/json" -Body '{"name":"Copper Ore"}'` |

## Items

An item is anything that can be crafted or used as an ingredient.

| Field | Type | Rules |
|---|---|---|
| `id` | whole number | Set by the server |
| `name` | text | Required, unique, 1 to 80 characters. Upper and lower case are treated the same |
| `description` | text or null | Optional, up to 500 characters |
| `category` | text or null | Optional, up to 40 characters |
| `icon` | text or null | Optional. One of the names listed by `GET /api/icons`; any other name is refused with 400. The picture is served at `/icons/{icon}.svg` |
| `kind` | `raw` or `crafted` | Worked out by the server: crafted when the item has a recipe |

### List items

```http
GET /api/items?kind=crafted&search=iron
```

| Parameter | Default | Meaning |
|---|---|---|
| `search` | none | Text to find in the name or description |
| `category` | none | Return only this category |
| `kind` | none | `raw` or `crafted` |
| `sort` | `name` | `name`, `category`, or `id` |
| `order` | `asc` | `asc` or `desc` |
| `page` | `1` | Page number, starting at 1 |
| `pageSize` | `20` | Items per page, 1 to 100 |

Response `200 OK`:

```json
{
  "items": [
    {
      "id": 10,
      "name": "Iron Ingot",
      "description": "A bar of smelted iron.",
      "category": "Component",
      "kind": "crafted"
    },
    {
      "id": 11,
      "name": "Sword",
      "description": "An iron blade on a wooden grip.",
      "category": "Weapon",
      "kind": "crafted"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  "totalPages": 1
}
```

The Sword matches because its description contains "iron". When nothing matches, `items` is an empty list and the status is still 200.

### Get one item

```http
GET /api/items/9
```

Response `200 OK`:

```json
{
  "id": 9,
  "name": "Stick",
  "description": "A handle or shaft.",
  "category": "Component",
  "kind": "crafted",
  "recipe": {
    "id": 2,
    "outputQuantity": 4,
    "craftSeconds": 2,
    "ingredients": [
      { "itemId": 8, "itemName": "Plank", "quantity": 2 }
    ]
  },
  "usedIn": [
    { "recipeId": 7, "itemId": 14, "itemName": "Arrow", "quantity": 1 },
    { "recipeId": 6, "itemId": 13, "itemName": "Bow", "quantity": 3 },
    { "recipeId": 5, "itemId": 12, "itemName": "Pickaxe", "quantity": 2 },
    { "recipeId": 4, "itemId": 11, "itemName": "Sword", "quantity": 1 },
    { "recipeId": 8, "itemId": 15, "itemName": "Torch", "quantity": 1 }
  ]
}
```

`recipe` answers "what goes into this?" and `usedIn` answers "what does this go into?". For a raw item, `recipe` is null.

### Insert: create an item

```http
POST /api/items
Content-Type: application/json

{
  "name": "Copper Ore",
  "description": "Rock that contains copper.",
  "category": "Raw Material"
}
```

Response `201 Created`, with the header `Location: http://localhost:5080/api/items/20`:

```json
{
  "id": 20,
  "name": "Copper Ore",
  "description": "Rock that contains copper.",
  "category": "Raw Material",
  "kind": "raw",
  "recipe": null,
  "usedIn": []
}
```

Spaces at the start and end of text are removed before saving.

### Update: replace every field

```http
PUT /api/items/20
Content-Type: application/json

{
  "name": "Copper Ore",
  "description": "Soft orange rock."
}
```

Response `200 OK`:

```json
{
  "id": 20,
  "name": "Copper Ore",
  "description": "Soft orange rock.",
  "category": null,
  "kind": "raw",
  "recipe": null,
  "usedIn": []
}
```

The category was left out of the request, so it was cleared. PUT replaces the whole item.

### Partial update: change some fields

```http
PATCH /api/items/20
Content-Type: application/json

{
  "category": "Ore"
}
```

Response `200 OK`:

```json
{
  "id": 20,
  "name": "Copper Ore",
  "description": "Soft orange rock.",
  "category": "Ore",
  "kind": "raw",
  "recipe": null,
  "usedIn": []
}
```

Only the category changed. To clear an optional field, send an empty string: `{ "description": "" }`.

| | PUT | PATCH |
|---|---|---|
| Fields that are sent | Saved | Saved |
| Fields that are left out | Cleared | Kept |
| Use it to | Save a whole form | Change one thing |

### Delete an item

```http
DELETE /api/items/20
```

Response `204 No Content`, with an empty body. The item's own recipe is deleted with it.

## Recipes

A recipe says what goes into an item and how many come out. An item has at most one recipe.

| Field | Type | Rules |
|---|---|---|
| `id` | whole number | Set by the server |
| `outputItemId` | whole number | Required. The item must exist and must not have a recipe yet. Cannot be changed later |
| `outputItemName` | text | Filled in by the server |
| `outputQuantity` | whole number | 1 to 1,000. How many one craft makes. Defaults to 1 |
| `craftSeconds` | whole number | 0 to 86,400. How long one craft takes. Defaults to 0 |
| `ingredients` | list | 1 to 20 entries, each item listed once |
| `ingredients[].itemId` | whole number | Required. The item must exist |
| `ingredients[].quantity` | whole number | 1 to 1,000. How many one craft uses |

### List recipes

```http
GET /api/recipes?uses=9
```

| Parameter | Default | Meaning |
|---|---|---|
| `uses` | none | Return only recipes that use the item with this id as an ingredient |
| `page` | `1` | Page number, starting at 1 |
| `pageSize` | `20` | Recipes per page, 1 to 100 |

Recipes are sorted by the name of the item they make. The response has the same paging fields as the list of items, and each entry has the same shape as the response below.

### Get one recipe

```http
GET /api/recipes/4
```

Response `200 OK`:

```json
{
  "id": 4,
  "outputItemId": 11,
  "outputItemName": "Sword",
  "outputQuantity": 1,
  "craftSeconds": 8,
  "ingredients": [
    { "itemId": 10, "itemName": "Iron Ingot", "quantity": 2 },
    { "itemId": 9, "itemName": "Stick", "quantity": 1 }
  ]
}
```

### Insert: create a recipe

This example first needs an item to make. `POST /api/items` with `{ "name": "Steel Ingot", "category": "Component" }` returns id 20.

```http
POST /api/recipes
Content-Type: application/json

{
  "outputItemId": 20,
  "outputQuantity": 2,
  "craftSeconds": 15,
  "ingredients": [
    { "itemId": 10, "quantity": 3 },
    { "itemId": 3, "quantity": 1 }
  ]
}
```

Response `201 Created`, with the header `Location: http://localhost:5080/api/recipes/13`:

```json
{
  "id": 13,
  "outputItemId": 20,
  "outputItemName": "Steel Ingot",
  "outputQuantity": 2,
  "craftSeconds": 15,
  "ingredients": [
    { "itemId": 3, "itemName": "Coal", "quantity": 1 },
    { "itemId": 10, "itemName": "Iron Ingot", "quantity": 3 }
  ]
}
```

`GET /api/items/20` now reports `"kind": "crafted"`. Nothing was written to the item: its kind is worked out from the recipe.

### Update: replace a recipe

```http
PUT /api/recipes/13
Content-Type: application/json

{
  "outputQuantity": 3,
  "craftSeconds": 20,
  "ingredients": [
    { "itemId": 10, "quantity": 4 },
    { "itemId": 2, "quantity": 1 }
  ]
}
```

Response `200 OK`:

```json
{
  "id": 13,
  "outputItemId": 20,
  "outputItemName": "Steel Ingot",
  "outputQuantity": 3,
  "craftSeconds": 20,
  "ingredients": [
    { "itemId": 10, "itemName": "Iron Ingot", "quantity": 4 },
    { "itemId": 2, "itemName": "Iron Ore", "quantity": 1 }
  ]
}
```

The list that is sent becomes the whole list. Coal was left out, so it was removed.

### Delete a recipe

```http
DELETE /api/recipes/13
```

Response `204 No Content`. The item it made is kept and becomes raw. Recipes that use that item are not affected.

## Ingredients

These endpoints change one line of a recipe and leave the rest alone.

### List a recipe's ingredients

```http
GET /api/recipes/13/ingredients
```

Response `200 OK`:

```json
[
  { "itemId": 10, "itemName": "Iron Ingot", "quantity": 4 },
  { "itemId": 2, "itemName": "Iron Ore", "quantity": 1 }
]
```

### Add an ingredient, or change its quantity

```http
PUT /api/recipes/13/ingredients/3
Content-Type: application/json

{
  "quantity": 2
}
```

Response `201 Created` when the recipe did not use the item before:

```json
{ "itemId": 3, "itemName": "Coal", "quantity": 2 }
```

Sending the request again with a different quantity returns `200 OK` with the new quantity. One address does both jobs, so a caller never has to check first whether the ingredient is already there.

### Remove an ingredient

```http
DELETE /api/recipes/13/ingredients/3
```

Response `204 No Content`.

| | Replace the recipe | Change one ingredient |
|---|---|---|
| Address | `PUT /api/recipes/{id}` | `PUT /api/recipes/{id}/ingredients/{itemId}` |
| Other ingredients | Removed unless they are sent again | Kept |
| Use it to | Save a whole recipe form | Adjust one line |

## Planning

The question the app exists to answer: to make a quantity of an item, what does it take?

### Plan an item

```http
GET /api/items/18/plan?quantity=3
```

| Parameter | Default | Meaning |
|---|---|---|
| `quantity` | `1` | How many units to make, 1 to 1,000,000 |

Response `200 OK`:

```json
{
  "itemId": 18,
  "itemName": "Tool Kit",
  "quantity": 3,
  "rawMaterials": [
    { "itemId": 3, "itemName": "Coal", "quantity": 15 },
    { "itemId": 2, "itemName": "Iron Ore", "quantity": 30 },
    { "itemId": 1, "itemName": "Log", "quantity": 3 }
  ],
  "steps": [
    { "itemId": 8, "itemName": "Plank", "needed": 6, "crafts": 3, "made": 6, "leftover": 0, "seconds": 6 },
    { "itemId": 10, "itemName": "Iron Ingot", "needed": 15, "crafts": 15, "made": 15, "leftover": 0, "seconds": 150 },
    { "itemId": 9, "itemName": "Stick", "needed": 9, "crafts": 3, "made": 12, "leftover": 3, "seconds": 6 },
    { "itemId": 12, "itemName": "Pickaxe", "needed": 3, "crafts": 3, "made": 3, "leftover": 0, "seconds": 24 },
    { "itemId": 11, "itemName": "Sword", "needed": 3, "crafts": 3, "made": 3, "leftover": 0, "seconds": 24 },
    { "itemId": 18, "itemName": "Tool Kit", "needed": 3, "crafts": 3, "made": 3, "leftover": 0, "seconds": 12 }
  ],
  "totalSeconds": 222
}
```

How to read it:

| Part | Meaning |
|---|---|
| `rawMaterials` | What to gather before starting, in name order |
| `steps` | What to craft, in the order to craft it. Each step comes after the steps it depends on |
| `needed` | Units required in total, by the target and by other steps |
| `crafts` | Times to run the recipe: `needed` divided by the recipe's output, rounded up |
| `made` and `leftover` | Sticks are made 4 at a time, so 9 needed means 3 crafts, 12 made, and 3 left over |
| `seconds` | Time for all the crafts of that step. `totalSeconds` is the sum |

Demand is totalled across every step before it is rounded. Sword and Pickaxe both need Sticks; rounding each separately would craft Sticks more often than necessary. Section 5 of the design document works through the numbers.

For a raw item, the plan is just the item itself:

```http
GET /api/items/1/plan?quantity=5
```

```json
{
  "itemId": 1,
  "itemName": "Log",
  "quantity": 5,
  "rawMaterials": [ { "itemId": 1, "itemName": "Log", "quantity": 5 } ],
  "steps": [],
  "totalSeconds": 0
}
```

### Export a plan as a file

```http
GET /api/items/18/plan/export?quantity=3
```

Response `200 OK` with `Content-Type: text/csv` and `Content-Disposition: attachment; filename=plan-tool-kit-x3.csv`, so a browser saves it as a file. The contents are one table that opens cleanly in a spreadsheet:

```
Type,Item,Quantity,Crafts,Made,Leftover,Seconds
Raw material,Coal,15,,,,
Raw material,Iron Ore,30,,,,
Raw material,Log,3,,,,
Step,Plank,6,3,6,0,6
Step,Iron Ingot,15,15,15,0,150
Step,Stick,9,3,12,3,6
Step,Pickaxe,3,3,3,0,24
Step,Sword,3,3,3,0,24
Step,Tool Kit,3,3,3,0,12
Total time,,,,,,222
```

The same rejections apply as for the plan. An item name that contains a comma is quoted, as CSV requires.

### Show an item's recipe tree

```http
GET /api/items/11/tree
```

Response `200 OK`:

```json
{
  "itemId": 11,
  "itemName": "Sword",
  "kind": "crafted",
  "quantity": 1,
  "outputQuantity": 1,
  "craftSeconds": 8,
  "ingredients": [
    {
      "itemId": 9,
      "itemName": "Stick",
      "kind": "crafted",
      "quantity": 1,
      "outputQuantity": 4,
      "craftSeconds": 2,
      "ingredients": [
        {
          "itemId": 8,
          "itemName": "Plank",
          "kind": "crafted",
          "quantity": 2,
          "outputQuantity": 2,
          "craftSeconds": 2,
          "ingredients": [
            { "itemId": 1, "itemName": "Log", "kind": "raw", "quantity": 1, "outputQuantity": null, "craftSeconds": null, "ingredients": [], "truncated": false }
          ],
          "truncated": false
        }
      ],
      "truncated": false
    },
    {
      "itemId": 10,
      "itemName": "Iron Ingot",
      "kind": "crafted",
      "quantity": 2,
      "outputQuantity": 1,
      "craftSeconds": 10,
      "ingredients": [
        { "itemId": 2, "itemName": "Iron Ore", "kind": "raw", "quantity": 2, "outputQuantity": null, "craftSeconds": null, "ingredients": [], "truncated": false },
        { "itemId": 3, "itemName": "Coal", "kind": "raw", "quantity": 1, "outputQuantity": null, "craftSeconds": null, "ingredients": [], "truncated": false }
      ],
      "truncated": false
    }
  ],
  "truncated": false
}
```

| | Plan | Tree |
|---|---|---|
| Shows | Totals | Structure |
| A shared item | Appears once, with its total | Appears under every recipe that uses it |
| `quantity` means | Units needed in all | Units one craft of the item above uses |
| Use it for | Deciding what to gather | Drawing the recipe |

A tree stops growing at 2,000 nodes. A node whose ingredients were left out has `"truncated": true`, and its own tree can be requested separately.

## Summary

### Totals and highlights

```http
GET /api/stats
```

Response `200 OK`:

```json
{
  "itemCount": 19,
  "rawItemCount": 7,
  "craftedItemCount": 12,
  "categoryCount": 6,
  "ingredientLineCount": 22,
  "mostUsedIngredient": { "itemId": 9, "itemName": "Stick", "recipeCount": 5 },
  "longestChain": { "itemId": 19, "itemName": "Hunter Kit", "depth": 4 }
}
```

`depth` counts recipes: a raw item is 0, an item made only from raw items is 1. Hunter Kit is 4 because its Bow needs Sticks, which need Planks, which need Logs. Tool Kit is also 4; ties are settled by name. Both highlights are null when there are no recipes.

## Icons

### List the pictures an item can be given

```http
GET /api/icons
```

Response `200 OK`, in name order:

```json
[
  { "name": "anvil", "author": "Lorc", "url": "/icons/anvil.svg" },
  { "name": "backpack", "author": "Delapouite", "url": "/icons/backpack.svg" },
  { "name": "broadhead-arrow", "author": "Lorc", "url": "/icons/broadhead-arrow.svg" },
  { "name": "broadsword", "author": "Lorc", "url": "/icons/broadsword.svg" }
]
```

An item's `icon` field takes one of these names. The pictures come from [game-icons.net](https://game-icons.net) under the Creative Commons Attribution 3.0 license, and `author` is who to credit. Each file is a single-colour SVG that takes the colour of the text around it when used as a mask.

## Rejections

Every rejection uses the Problem Details format, with the content type `application/problem+json`.

| Field | Meaning |
|---|---|
| `title` | Short name for the kind of problem |
| `status` | The HTTP status code |
| `detail` | Plain explanation of this case |
| `errors` | The fields at fault and what is wrong with each. Present on 400 responses |
| `traceId` | Identifies the request in the server's log. Different every time |

| Status | Meaning | What to do |
|---|---|---|
| 400 Bad Request | The request itself is wrong | Fix the input and send it again |
| 404 Not Found | The record or address does not exist | Check the id |
| 409 Conflict | The request is valid but clashes with existing data | Change the data it clashes with, or change the request |

### 400: a field is missing or not valid

```http
POST /api/items
Content-Type: application/json

{
  "name": "An item name that is far too long, because it has more than eighty characters in it"
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "name": ["The name must be 1 to 80 characters long."]
  },
  "traceId": "00-5d495e0df97f7408a708bf6e9d3deb78-096a5caf4cae5ab7-00"
}
```

The same format is used for values in the address. `GET /api/items?pageSize=500` returns:

```json
"errors": {
  "pageSize": ["The field pageSize must be between 1 and 100."]
}
```

### 404: the item does not exist

```http
GET /api/items/9999
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "The item was not found",
  "status": 404,
  "detail": "There is no item with id 9999.",
  "traceId": "00-dad922c18eaaaacb11d56b09d050b97e-841363442bd0e6e8-00"
}
```

### 409: the name is already in use

```http
POST /api/items
Content-Type: application/json

{
  "name": "stick"
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The name is already in use",
  "status": 409,
  "detail": "Another item is already named \"stick\". Item names must be unique.",
  "traceId": "00-616d7105f4ce01cec8a54386e851bb97-cf109848345500d8-00"
}
```

"stick" clashes with "Stick" because names ignore upper and lower case.

### 409: the item is in use

```http
DELETE /api/items/9
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The item is in use",
  "status": 409,
  "detail": "\"Stick\" is an ingredient of: Arrow, Bow, Pickaxe, Sword, Torch. Remove it from those recipes first.",
  "traceId": "00-aed217991542f610435bdcee7a235983-9c20af46a14eac77-00",
  "usedIn": [
    { "recipeId": 7, "itemId": 14, "itemName": "Arrow", "quantity": 1 },
    { "recipeId": 6, "itemId": 13, "itemName": "Bow", "quantity": 3 },
    { "recipeId": 5, "itemId": 12, "itemName": "Pickaxe", "quantity": 2 },
    { "recipeId": 4, "itemId": 11, "itemName": "Sword", "quantity": 1 },
    { "recipeId": 8, "itemId": 15, "itemName": "Torch", "quantity": 1 }
  ]
}
```

The extra `usedIn` field lists what depends on the item, so a program can act on it without reading the sentence in `detail`.

### 400: problems inside a list

```http
POST /api/recipes
Content-Type: application/json

{
  "outputItemId": 21,
  "ingredients": [
    { "itemId": 3, "quantity": 1 },
    { "itemId": 3, "quantity": 2 },
    { "itemId": 777, "quantity": 1 }
  ]
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "ingredients[1].itemId": ["Item 3 is listed more than once. List each item once, with its total quantity."],
    "ingredients[2].itemId": ["There is no item with id 777."]
  },
  "traceId": "00-1c62714401c490f851f3eee70e9a130c-6eab3d79b338978b-00"
}
```

Each problem is reported against its position in the list, counting from 0. Every problem is reported at once, so the caller can fix them in one pass.

### 409: the recipe would loop

The sample data says a Stick is made from Planks. This request tries to make Planks from Sticks:

```http
PUT /api/recipes/1
Content-Type: application/json

{
  "outputQuantity": 2,
  "ingredients": [
    { "itemId": 9, "quantity": 1 }
  ]
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The recipe would loop",
  "status": 409,
  "detail": "These ingredients would create a loop: \"Plank\" needs \"Stick\", and \"Stick\" needs \"Plank\".",
  "traceId": "00-39fa2f9beeb2699a97012831281b1592-4872561e724db073-00",
  "loop": ["Plank", "Stick", "Plank"]
}
```

The `loop` field shows the chain, in which each item needs the next. Loops are found at any depth. Trying to make a Log from a Tool Kit returns:

```json
"loop": ["Log", "Tool Kit", "Sword", "Stick", "Plank", "Log"]
```

### 409: the item already has a recipe

```http
POST /api/recipes
Content-Type: application/json

{
  "outputItemId": 20,
  "ingredients": [
    { "itemId": 3, "quantity": 1 }
  ]
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The item already has a recipe",
  "status": 409,
  "detail": "\"Steel Ingot\" is already made by recipe 13. An item has one recipe, so replace that recipe instead of creating another.",
  "traceId": "00-5c03fa6c8d872cc5c0aa5c8657646918-00b1fd3cc4595928-00",
  "recipeId": 13
}
```

The `recipeId` field gives the recipe to replace.

### 409: the recipe needs an ingredient

```http
DELETE /api/recipes/13/ingredients/10
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The recipe needs an ingredient",
  "status": 409,
  "detail": "Item 10 is the only ingredient of recipe 13, and a recipe needs at least one. To remove it, delete the recipe.",
  "traceId": "00-b245f0da41069bccd65c728dd1776472-ca32002e2a1c88ea-00"
}
```

## Every rejection at a glance

| Status | Title | Returned when |
|---|---|---|
| 400 | One or more validation errors occurred. | A field is missing, out of range, or refers to an item that does not exist |
| 400 | The plan is too large | A total in the plan would not fit in a 64-bit number. The `itemId` field names the item. Try a smaller quantity |
| 404 | The item was not found | No item has the id in the address |
| 404 | The recipe was not found | No recipe has the id in the address |
| 404 | The ingredient was not found | The recipe does not use that item |
| 404 | Not Found | The address does not exist |
| 409 | The name is already in use | Another item has that name |
| 409 | The item is in use | Other recipes use the item as an ingredient |
| 409 | The item already has a recipe | A second recipe was created for one item |
| 409 | The recipe would loop | An ingredient leads back to the item being made |
| 409 | The recipe needs an ingredient | The last ingredient was removed |
| 409 | The recipe is full | A 21st ingredient was added |
