# Data Dictionary

Every table and field in the database, with its type, meaning, and rules. The database is one SQLite file, `craftingplanner.db`, created by the app on first run from the classes in `src/CraftingPlanner.Api/Models` and the rules in `Data/PlannerDbContext.cs`.

## How the tables relate

```
Items 1 ──── 0..1 Recipes                 an item has at most one recipe
Recipes 1 ──── 1..* RecipeIngredients     a recipe has one line per ingredient
Items 1 ──── 0..* RecipeIngredients       an item can be an ingredient of many recipes
ShoppingLists 1 ──── 0..* ShoppingListEntries   a list has one line per item on it
Items 1 ──── 0..* ShoppingListEntries     an item can be on many lists
```

An item is **raw** when no row in Recipes has it as `OutputItemId`, and **crafted** when one does. That is worked out on request and never stored.

## Items

One row per item: anything that can be crafted or used as an ingredient.

| Field | Type | Null | Rules | Meaning |
|---|---|---|---|---|
| `Id` | INTEGER | no | Primary key, assigned by the database, never reused | Identifies the item |
| `Name` | TEXT | no | Unique, ignoring letter case. 1 to 80 characters. Spaces at the ends are removed before saving | Display name |
| `Description` | TEXT | yes | Up to 500 characters | What the item is |
| `Category` | TEXT | yes | Up to 40 characters, compared ignoring letter case | Group used for filtering, such as "Tool" |
| `Icon` | TEXT | yes | Up to 40 characters; lower-case letters, digits, and hyphens. The API only accepts names of pictures the website ships with | Name of the item's picture, served at `/icons/{Icon}.svg` |

Indexes: `IX_Items_Name`, unique.

When an item is deleted, its own recipe is deleted with it (cascade). An item that any recipe uses as an ingredient cannot be deleted (restrict); the API answers 409 and lists those recipes.

## Recipes

One row per recipe: how one item is made.

| Field | Type | Null | Rules | Meaning |
|---|---|---|---|---|
| `Id` | INTEGER | no | Primary key, assigned by the database | Identifies the recipe |
| `OutputItemId` | INTEGER | no | Refers to `Items.Id`. Unique, so an item has at most one recipe. Cannot be changed after creation | The item this recipe makes |
| `OutputQuantity` | INTEGER | no | 1 to 1,000 (database check constraint) | Units one craft makes |
| `CraftSeconds` | INTEGER | no | 0 or more (database check constraint); the API also caps it at 86,400 | Time for one craft |

Indexes: `IX_Recipes_OutputItemId`, unique.

When a recipe is deleted, its ingredient lines are deleted with it (cascade). The item it made is kept and becomes raw.

## RecipeIngredients

One row per ingredient line: "this recipe uses this many of that item".

| Field | Type | Null | Rules | Meaning |
|---|---|---|---|---|
| `RecipeId` | INTEGER | no | Part of the primary key. Refers to `Recipes.Id` | The recipe the line belongs to |
| `ItemId` | INTEGER | no | Part of the primary key. Refers to `Items.Id`. Cannot be the recipe's own output item, directly or through other recipes | The ingredient |
| `Quantity` | INTEGER | no | 1 to 1,000 (database check constraint) | Units one craft uses up |

The primary key is (`RecipeId`, `ItemId`), so an item appears at most once in a recipe. The API also limits a recipe to 20 lines.

Indexes: `IX_RecipeIngredients_ItemId`, for "what is this item used in?" lookups.

## ShoppingLists

One row per saved list of items to make.

| Field | Type | Null | Rules | Meaning |
|---|---|---|---|---|
| `Id` | INTEGER | no | Primary key, assigned by the database | Identifies the list |
| `Name` | TEXT | no | Unique, ignoring letter case. 1 to 80 characters | Display name |
| `Description` | TEXT | yes | Up to 500 characters | What the list is for |

Indexes: `IX_ShoppingLists_Name`, unique.

When a list is deleted, its entries are deleted with it (cascade). The items on it are not affected.

## ShoppingListEntries

One row per item on a list: "make this many of that item".

| Field | Type | Null | Rules | Meaning |
|---|---|---|---|---|
| `ListId` | INTEGER | no | Part of the primary key. Refers to `ShoppingLists.Id` | The list |
| `ItemId` | INTEGER | no | Part of the primary key. Refers to `Items.Id` | The item to make |
| `Quantity` | INTEGER | no | 1 to 1,000,000 (database check constraint) | Units to make |

The primary key is (`ListId`, `ItemId`), so an item appears at most once on a list. The API also limits a list to 50 entries.

When an item is deleted, its entries are deleted with it (cascade): the item is simply taken off every list. This differs from recipes on purpose; a list missing an entry is still a valid list, while a recipe missing an ingredient is wrong data.

## Rules enforced where

| Rule | Database | API |
|---|---|---|
| Names unique | Unique index | Checked first, so the caller gets a clear 409 |
| One recipe per item | Unique index | Checked first, 409 naming the existing recipe |
| Quantities within range | Check constraints | Checked first, 400 naming the field |
| Ingredient item exists | Foreign key | Checked first, 400 naming the position in the list |
| Cannot delete an item in use | Foreign key, restrict | Checked first, 409 listing the recipes |
| No looping recipes | Not expressible in SQLite | Loop search on every save, 409 showing the loop |
| At most 20 ingredients, craft time at most one day | Not enforced | 400 or 409 |
| List names unique | Unique index | Checked first, 409 |
| List quantities within range | Check constraint | Checked first, 400 |
| At most 50 items on a list | Not enforced | 409 |

The database rules are a backstop. The API checks everything first so that callers get a plain explanation instead of a database error.

## The tables as SQL

This is the script the app runs on first start, generated from the model classes.

```sql
CREATE TABLE "Items" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Items" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT COLLATE NOCASE NOT NULL,
    "Description" TEXT NULL,
    "Category" TEXT COLLATE NOCASE NULL,
    "Icon" TEXT NULL
);

CREATE TABLE "Recipes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Recipes" PRIMARY KEY AUTOINCREMENT,
    "OutputItemId" INTEGER NOT NULL,
    "OutputQuantity" INTEGER NOT NULL,
    "CraftSeconds" INTEGER NOT NULL,
    CONSTRAINT "CK_Recipes_CraftSeconds" CHECK ("CraftSeconds" >= 0),
    CONSTRAINT "CK_Recipes_OutputQuantity" CHECK ("OutputQuantity" BETWEEN 1 AND 1000),
    CONSTRAINT "FK_Recipes_Items_OutputItemId" FOREIGN KEY ("OutputItemId") REFERENCES "Items" ("Id") ON DELETE CASCADE
);

CREATE TABLE "RecipeIngredients" (
    "RecipeId" INTEGER NOT NULL,
    "ItemId" INTEGER NOT NULL,
    "Quantity" INTEGER NOT NULL,
    CONSTRAINT "PK_RecipeIngredients" PRIMARY KEY ("RecipeId", "ItemId"),
    CONSTRAINT "CK_RecipeIngredients_Quantity" CHECK ("Quantity" BETWEEN 1 AND 1000),
    CONSTRAINT "FK_RecipeIngredients_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Items" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RecipeIngredients_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ShoppingLists" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ShoppingLists" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT COLLATE NOCASE NOT NULL,
    "Description" TEXT NULL
);

CREATE TABLE "ShoppingListEntries" (
    "ListId" INTEGER NOT NULL,
    "ItemId" INTEGER NOT NULL,
    "Quantity" INTEGER NOT NULL,
    CONSTRAINT "PK_ShoppingListEntries" PRIMARY KEY ("ListId", "ItemId"),
    CONSTRAINT "CK_ShoppingListEntries_Quantity" CHECK ("Quantity" BETWEEN 1 AND 1000000),
    CONSTRAINT "FK_ShoppingListEntries_Items_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Items" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ShoppingListEntries_ShoppingLists_ListId" FOREIGN KEY ("ListId") REFERENCES "ShoppingLists" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_Items_Name" ON "Items" ("Name");
CREATE INDEX "IX_RecipeIngredients_ItemId" ON "RecipeIngredients" ("ItemId");
CREATE UNIQUE INDEX "IX_Recipes_OutputItemId" ON "Recipes" ("OutputItemId");
CREATE INDEX "IX_ShoppingListEntries_ItemId" ON "ShoppingListEntries" ("ItemId");
CREATE UNIQUE INDEX "IX_ShoppingLists_Name" ON "ShoppingLists" ("Name");
```

## Sample data

On first run the app adds 19 items and 12 recipes: raw materials (Log, Iron Ore, Coal, Cobblestone, String, Flint, Feather), components (Plank, Stick, Iron Ingot), finished items (Sword, Pickaxe, Bow, Arrow, Torch, Furnace, Chest), and two kits (Tool Kit, Hunter Kit). Each has an icon. The sample data is only added when the Items table is empty, so changes are never overwritten. To start over, stop the app and delete the database file.

## When the tables change

The app creates the tables on first run but never alters an existing file. At startup it compares the file with the code, and if a column is missing (for example, a file made before the `Icon` column existed) it stops with a message naming the column and asking for the file to be deleted. A deployed app would use migrations to upgrade the file in place; the design document explains why this project does not.
