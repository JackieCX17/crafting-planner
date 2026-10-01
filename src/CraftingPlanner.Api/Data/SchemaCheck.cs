using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CraftingPlanner.Api.Data;

/// <summary>
/// Compares the database file with what the code expects. The app creates its tables on
/// first run but never alters existing ones, so a file made by an older version of the app
/// can lack a column or a table. This check finds that out at startup, so the app can say
/// so plainly instead of failing on the first request.
/// </summary>
/// <remarks>
/// A deployed app would use versioned schema changes (migrations) instead, which upgrade a
/// database in place. The design document records why this project does not.
/// </remarks>
public static class SchemaCheck
{
    /// <summary>Finds the columns the code expects that the database file does not have.</summary>
    /// <param name="db">The database.</param>
    /// <returns>Missing columns as "Table.Column". Empty when the file matches the code.</returns>
    public static List<string> FindMissingColumns(PlannerDbContext db)
    {
        var missing = new List<string>();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null)
            {
                continue;
            }

            // pragma_table_info lists a table's columns; it is empty for a table that does not exist.
            // SqlQuery turns the table name into a parameter rather than pasting it into the SQL.
            var existing = db.Database
                .SqlQuery<string>($"SELECT name AS Value FROM pragma_table_info({table})")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var storeObject = StoreObjectIdentifier.Table(table);
            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(storeObject);
                if (column is not null && !existing.Contains(column))
                {
                    missing.Add($"{table}.{column}");
                }
            }
        }

        return missing;
    }
}
