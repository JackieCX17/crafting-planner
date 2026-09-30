# Crafting Planner API Guide

This guide shows every operation with an example request and the response it returns. The interactive API page at <http://localhost:5080/swagger> is the live reference. This guide is the walkthrough.

**Covered so far:** items (stage 1). Recipes, planning, and summaries are added in later stages.

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
