// Initializes and configures SQLite database access.
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;

namespace ReachIT.Infrastructure.Persistence;

public sealed class DatabaseService : IDatabaseService
{
    private static readonly string ResolvedDatabasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReachIT",
        "reachit.db");

    public string DatabasePath => ResolvedDatabasePath;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(ResolvedDatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var requiresRebuild = RequiresSchemaRebuild();

        using var dbContext = CreateDbContext();
        if (requiresRebuild)
        {
            BackupLegacyDatabase();
            dbContext.Database.EnsureDeleted();
        }

        dbContext.Database.EnsureCreated();

        return Task.CompletedTask;
    }

    public ReachItDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ReachItDbContext>()
            .UseSqlite($"Data Source={ResolvedDatabasePath}")
            .EnableSensitiveDataLogging(false)
            .EnableDetailedErrors(false)
            .Options;

        return new ReachItDbContext(options);
    }

    private bool RequiresSchemaRebuild()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return false;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        if (!TableExists(connection, "Projects"))
        {
            return true;
        }

        if (!ColumnExists(connection, "Projects", "Description") ||
            !ColumnExists(connection, "Projects", "ProjectDirectoryPath") ||
            !ColumnExists(connection, "Projects", "TemplateType"))
        {
            return true;
        }

        var requiredTables = new[]
        {
            "ProjectTreeNodes",
            "ExternalResources",
            "RecentExternalFiles"
        };

        return requiredTables.Any(table => !TableExists(connection, table));
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result) > 0;
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info([{tableName}]);";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var existingColumnName = reader.GetString(1);
            if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void BackupLegacyDatabase()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        var backupPath = Path.Combine(
            Path.GetDirectoryName(ResolvedDatabasePath) ?? string.Empty,
            $"reachit_legacy_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db.bak");

        File.Copy(ResolvedDatabasePath, backupPath, overwrite: false);
    }
}
