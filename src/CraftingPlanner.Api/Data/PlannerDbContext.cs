using CraftingPlanner.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CraftingPlanner.Api.Data;

/// <summary>
/// The app's connection to the database. Each property is one table, and
/// <see cref="OnModelCreating"/> describes the rules the database itself enforces.
/// </summary>
/// <param name="options">Settings such as which database file to open. Supplied at startup.</param>
public class PlannerDbContext(DbContextOptions<PlannerDbContext> options) : DbContext(options)
{
    /// <summary>The Items table.</summary>
    public DbSet<Item> Items => Set<Item>();

    /// <summary>The Recipes table.</summary>
    public DbSet<Recipe> Recipes => Set<Recipe>();

    /// <summary>The RecipeIngredients table.</summary>
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    /// <summary>The ShoppingLists table.</summary>
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();

    /// <summary>The ShoppingListEntries table.</summary>
    public DbSet<ShoppingListEntry> ShoppingListEntries => Set<ShoppingListEntry>();

    /// <summary>
    /// Describes the tables: field lengths, uniqueness, how tables link together,
    /// and what happens to linked rows when a row is deleted.
    /// </summary>
    /// <param name="modelBuilder">The object the table descriptions are added to.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>(item =>
        {
            // NOCASE makes comparisons ignore upper and lower case,
            // so "stick" and "Stick" count as the same name.
            item.Property(i => i.Name).HasMaxLength(80).UseCollation("NOCASE");
            item.HasIndex(i => i.Name).IsUnique();

            item.Property(i => i.Description).HasMaxLength(500);
            item.Property(i => i.Category).HasMaxLength(40).UseCollation("NOCASE");
            item.Property(i => i.Icon).HasMaxLength(40);
        });

        modelBuilder.Entity<Recipe>(recipe =>
        {
            // One item, one recipe. Deleting the item deletes its recipe,
            // because a recipe has no meaning without the item it makes.
            recipe.HasOne(r => r.OutputItem)
                .WithOne(i => i.Recipe)
                .HasForeignKey<Recipe>(r => r.OutputItemId)
                .OnDelete(DeleteBehavior.Cascade);

            recipe.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Recipes_OutputQuantity", "\"OutputQuantity\" BETWEEN 1 AND 1000");
                table.HasCheckConstraint("CK_Recipes_CraftSeconds", "\"CraftSeconds\" >= 0");
            });
        });

        modelBuilder.Entity<RecipeIngredient>(line =>
        {
            line.HasKey(l => new { l.RecipeId, l.ItemId });

            // Deleting a recipe deletes its lines.
            line.HasOne(l => l.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(l => l.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting an item that a recipe still uses is refused,
            // because it would silently break that recipe.
            line.HasOne(l => l.Item)
                .WithMany(i => i.UsedIn)
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            line.ToTable(table =>
                table.HasCheckConstraint("CK_RecipeIngredients_Quantity", "\"Quantity\" BETWEEN 1 AND 1000"));
        });

        modelBuilder.Entity<ShoppingList>(list =>
        {
            list.Property(l => l.Name).HasMaxLength(80).UseCollation("NOCASE");
            list.HasIndex(l => l.Name).IsUnique();
            list.Property(l => l.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<ShoppingListEntry>(entry =>
        {
            entry.HasKey(e => new { e.ListId, e.ItemId });

            // Deleting a list deletes its lines.
            entry.HasOne(e => e.List)
                .WithMany(l => l.Entries)
                .HasForeignKey(e => e.ListId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting an item removes it from every list. Unlike a recipe, a list
            // missing one of its lines is still a valid list.
            entry.HasOne(e => e.Item)
                .WithMany(i => i.OnLists)
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entry.ToTable(table =>
                table.HasCheckConstraint("CK_ShoppingListEntries_Quantity", "\"Quantity\" BETWEEN 1 AND 1000000"));
        });
    }
}
