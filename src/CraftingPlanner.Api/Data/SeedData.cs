using CraftingPlanner.Api.Models;

namespace CraftingPlanner.Api.Data;

/// <summary>
/// Sample items and recipes, added the first time the app runs so that
/// there is something to look at straight away.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Adds the sample data when the Items table is empty. Does nothing otherwise,
    /// so data that has been changed is never overwritten.
    /// </summary>
    /// <param name="db">The database to fill.</param>
    public static void AddIfEmpty(PlannerDbContext db)
    {
        if (db.Items.Any())
        {
            return;
        }

        // Raw materials: gathered, not crafted.
        var log = NewItem("Log", "Raw Material", "Wood cut from a tree.");
        var ironOre = NewItem("Iron Ore", "Raw Material", "Rock that contains iron.");
        var coal = NewItem("Coal", "Raw Material", "Fuel for smelting and lighting.");
        var cobblestone = NewItem("Cobblestone", "Raw Material", "Rough stone.");
        var thread = NewItem("String", "Raw Material", "Thin cord.");
        var flint = NewItem("Flint", "Raw Material", "A hard stone that splits into sharp edges.");
        var feather = NewItem("Feather", "Raw Material", "Keeps an arrow flying straight.");

        // Components: crafted, then used to craft other things.
        var plank = NewItem("Plank", "Component", "A board sawn from a log.");
        var stick = NewItem("Stick", "Component", "A handle or shaft.");
        var ironIngot = NewItem("Iron Ingot", "Component", "A bar of smelted iron.");

        // Finished items.
        var sword = NewItem("Sword", "Weapon", "An iron blade on a wooden grip.");
        var pickaxe = NewItem("Pickaxe", "Tool", "Breaks stone and ore.");
        var bow = NewItem("Bow", "Weapon", "Fires arrows.");
        var arrow = NewItem("Arrow", "Weapon", "Ammunition for a bow.");
        var torch = NewItem("Torch", "Utility", "A portable light.");
        var furnace = NewItem("Furnace", "Utility", "Smelts ore into ingots.");
        var chest = NewItem("Chest", "Utility", "Stores items.");

        // Kits: finished items bundled together. These have the longest chains.
        var toolKit = NewItem("Tool Kit", "Kit", "A sword and a pickaxe.");
        var hunterKit = NewItem("Hunter Kit", "Kit", "A bow with arrows, packed in a chest.");

        db.Items.AddRange(
            log, ironOre, coal, cobblestone, thread, flint, feather,
            plank, stick, ironIngot,
            sword, pickaxe, bow, arrow, torch, furnace, chest,
            toolKit, hunterKit);

        db.Recipes.AddRange(
            NewRecipe(plank, makes: 2, seconds: 2, (log, 1)),
            NewRecipe(stick, makes: 4, seconds: 2, (plank, 2)),
            NewRecipe(ironIngot, makes: 1, seconds: 10, (ironOre, 2), (coal, 1)),
            NewRecipe(sword, makes: 1, seconds: 8, (ironIngot, 2), (stick, 1)),
            NewRecipe(pickaxe, makes: 1, seconds: 8, (ironIngot, 3), (stick, 2)),
            NewRecipe(bow, makes: 1, seconds: 6, (stick, 3), (thread, 3)),
            NewRecipe(arrow, makes: 4, seconds: 3, (flint, 1), (stick, 1), (feather, 1)),
            NewRecipe(torch, makes: 4, seconds: 1, (coal, 1), (stick, 1)),
            NewRecipe(furnace, makes: 1, seconds: 12, (cobblestone, 8)),
            NewRecipe(chest, makes: 1, seconds: 6, (plank, 8)),
            NewRecipe(toolKit, makes: 1, seconds: 4, (sword, 1), (pickaxe, 1)),
            NewRecipe(hunterKit, makes: 1, seconds: 4, (bow, 1), (arrow, 16), (chest, 1)));

        db.SaveChanges();
    }

    /// <summary>Builds an item that has not been saved yet.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="category">Group the item belongs to.</param>
    /// <param name="description">What the item is.</param>
    /// <returns>The new item.</returns>
    private static Item NewItem(string name, string category, string description) =>
        new() { Name = name, Category = category, Description = description };

    /// <summary>Builds a recipe that has not been saved yet.</summary>
    /// <param name="output">The item the recipe makes.</param>
    /// <param name="makes">How many units one craft makes.</param>
    /// <param name="seconds">How long one craft takes.</param>
    /// <param name="ingredients">Each ingredient with the quantity one craft uses.</param>
    /// <returns>The new recipe.</returns>
    private static Recipe NewRecipe(
        Item output,
        int makes,
        int seconds,
        params (Item Item, int Quantity)[] ingredients) =>
        new()
        {
            OutputItem = output,
            OutputQuantity = makes,
            CraftSeconds = seconds,
            Ingredients = ingredients
                .Select(ingredient => new RecipeIngredient
                {
                    Item = ingredient.Item,
                    Quantity = ingredient.Quantity,
                })
                .ToList(),
        };
}
