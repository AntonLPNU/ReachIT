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
        EnsureAppSettingsLanguageColumn();
        EnsureWorkProgressTables();

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

        if (!ColumnExists(connection, "AppSettings", "Theme") ||
            !ColumnExists(connection, "AppSettings", "EnableNotifications") ||
            !ColumnExists(connection, "AppSettings", "ShowFloatingLogoOnStartup") ||
            !ColumnExists(connection, "AppSettings", "FloatingLogoLeft") ||
            !ColumnExists(connection, "AppSettings", "FloatingLogoTop") ||
            !ColumnExists(connection, "AppSettings", "LastOpenedProjectPath") ||
            !ColumnExists(connection, "AppSettings", "FloatingLogoHotkey") ||
            !ColumnExists(connection, "AppSettings", "QuickAddTaskHotkey") ||
            !ColumnExists(connection, "AppSettings", "FocusModeHotkey") ||
            !ColumnExists(connection, "AppSettings", "MainWindowHotkey"))
        {
            return true;
        }

        var requiredTables = new[]
        {
            "ProjectTreeNodes",
            "ExternalResources",
            "RecentExternalFiles",
            "Tasks",
            "TaskHistoryEntries"
        };

        if (requiredTables.Any(table => !TableExists(connection, table)))
        {
            return true;
        }

        if (!ColumnExists(connection, "Tasks", "AttachedFilePath") ||
            !ColumnExists(connection, "Tasks", "Status") ||
            !ColumnExists(connection, "Tasks", "Priority") ||
            !ColumnExists(connection, "Tasks", "ParentTaskId"))
        {
            return true;
        }

        return false;
    }

    private static void EnsureAppSettingsLanguageColumn()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        if (!TableExists(connection, "AppSettings") || ColumnExists(connection, "AppSettings", "Language"))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "ALTER TABLE AppSettings ADD COLUMN Language TEXT NOT NULL DEFAULT 'en';";
        command.ExecuteNonQuery();
    }

    private static void EnsureWorkProgressTables()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS WorkItems (
                Id TEXT NOT NULL CONSTRAINT PK_WorkItems PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                ParentId TEXT NULL,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                Type INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                Priority INTEGER NOT NULL,
                ProgressPercent REAL NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                StartedAt TEXT NULL,
                CompletedAt TEXT NULL,
                Deadline TEXT NULL,
                EstimatedWorkUnits REAL NOT NULL,
                CompletedWorkUnits REAL NOT NULL,
                LinkedPath TEXT NOT NULL DEFAULT '',
                LinkedApp TEXT NOT NULL DEFAULT '',
                Tags TEXT NOT NULL DEFAULT '',
                Notes TEXT NOT NULL DEFAULT '',
                MilestoneId TEXT NULL,
                LegacyTaskItemId TEXT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS WorkUnits (
                Id TEXT NOT NULL CONSTRAINT PK_WorkUnits PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                WorkItemId TEXT NULL,
                Type INTEGER NOT NULL,
                Value REAL NOT NULL,
                Source TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                MetadataJson TEXT NOT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS Milestones (
                Id TEXT NOT NULL CONSTRAINT PK_Milestones PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                Deadline TEXT NULL,
                Status INTEGER NOT NULL,
                ProgressPercent REAL NOT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS TaskSuggestions (
                Id TEXT NOT NULL CONSTRAINT PK_TaskSuggestions PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                SuggestedTitle TEXT NOT NULL,
                SuggestedDescription TEXT NOT NULL DEFAULT '',
                SuggestedType INTEGER NOT NULL,
                SuggestedLinkedPath TEXT NOT NULL DEFAULT '',
                Confidence REAL NOT NULL,
                Reason TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL,
                Status INTEGER NOT NULL
            );
            """);

        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_WorkItems_ProjectId_LegacyTaskItemId ON WorkItems (ProjectId, LegacyTaskItemId);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_WorkItems_ProjectId_LinkedPath ON WorkItems (ProjectId, LinkedPath);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_WorkUnits_ProjectId_WorkItemId ON WorkUnits (ProjectId, WorkItemId);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_WorkUnits_ProjectId_CreatedAt ON WorkUnits (ProjectId, CreatedAt);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_TaskSuggestions_ProjectId_Status ON TaskSuggestions (ProjectId, Status);");
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
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
