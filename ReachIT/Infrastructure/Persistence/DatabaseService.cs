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
        EnsureProjectActivitySettingsColumns();
        EnsureActivitySettingsColumns();
        EnsureTaskQueueColumns();
        EnsureTaskFileLinkTables();
        EnsureAccountTables();
        EnsureWorkProgressTables();
        EnsureActivityTables();

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

    private static void EnsureProjectActivitySettingsColumns()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        if (!TableExists(connection, "Projects"))
        {
            return;
        }

        AddColumnIfMissing(connection, "Projects", "UseProjectActivitySettings", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "Projects", "ProjectEnableActivityTracking", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "Projects", "ProjectTrackActiveWindow", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "Projects", "ProjectTrackFileChanges", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "Projects", "ProjectTrackGitChanges", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "Projects", "ProjectTrackTextStatistics", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "Projects", "ProjectPauseActivityTracking", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "Projects", "ProjectIgnoredFoldersSerialized", "TEXT NOT NULL DEFAULT 'bin;obj;.git;.vs;node_modules;packages;build;dist'");
    }

    private static void EnsureActivitySettingsColumns()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        if (!TableExists(connection, "AppSettings"))
        {
            return;
        }

        AddColumnIfMissing(connection, "AppSettings", "EnableActivityTracking", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "AppSettings", "AllowedApplicationsSerialized", "TEXT NOT NULL DEFAULT 'ReachIT;explorer;SearchHost;ShellExperienceHost;StartMenuExperienceHost;ApplicationFrameHost;Code;Cursor;devenv;rider64;idea64;pycharm64;webstorm64;clion64;datagrip64;phpstorm64;eclipse;notepad;notepad++;Notepad;WINWORD;EXCEL;POWERPNT;OUTLOOK;OneNote;Acrobat;FoxitPDFEditor;chrome;msedge;firefox;brave;FreeCAD;blender;Blockbench;Aseprite;Resolve;fusion360;acad;SketchUp;3dsmax;Maya;Photoshop;Illustrator;figma;inkscape;gimp;paintdotnet;PaintStudio.View;WindowsTerminal;wt;powershell;cmd;SnippingTool;ScreenClippingHost;Snipaste;ShareX;Lightshot;Greenshot;git-bash;putty;winscp;postman;insomnia;docker desktop;Docker Desktop;Spotify;Music.UI;iTunes;slack;Teams;Zoom'");
        AddColumnIfMissing(connection, "AppSettings", "FocusDistractingApplicationsSerialized", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "AppSettings", "TrackActiveWindow", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "AppSettings", "TrackFileChanges", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "AppSettings", "TrackGitChanges", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "AppSettings", "TrackTextStatistics", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "AppSettings", "IgnorePrivateApps", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "AppSettings", "PauseActivityTracking", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "AppSettings", "PrivateAppsSerialized", "TEXT NOT NULL DEFAULT '1password;bitwarden;keepass;authenticator'");
        AddColumnIfMissing(connection, "AppSettings", "IgnoredFoldersSerialized", "TEXT NOT NULL DEFAULT 'bin;obj;.git;.vs;node_modules;packages;build;dist'");
    }

    private static void EnsureTaskQueueColumns()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        if (!TableExists(connection, "Tasks"))
        {
            return;
        }

        AddColumnIfMissing(connection, "Tasks", "StartedAtUtc", "TEXT NULL");
        AddColumnIfMissing(connection, "Tasks", "CompletedAtUtc", "TEXT NULL");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_Tasks_IsCompleted_Priority ON Tasks (IsCompleted, Priority);");
    }

    private static void EnsureTaskFileLinkTables()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS TaskFileLinks (
                Id TEXT NOT NULL CONSTRAINT PK_TaskFileLinks PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                TaskItemId TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                IsDirectory INTEGER NOT NULL DEFAULT 0,
                LinkedAtUtc TEXT NOT NULL,
                LinkSource TEXT NOT NULL DEFAULT 'Manual',
                CONSTRAINT FK_TaskFileLinks_Tasks_TaskItemId FOREIGN KEY (TaskItemId) REFERENCES Tasks (Id) ON DELETE CASCADE
            );
            """);

        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_TaskFileLinks_ProjectId_TaskItemId ON TaskFileLinks (ProjectId, TaskItemId);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_TaskFileLinks_ProjectId_FilePath ON TaskFileLinks (ProjectId, FilePath);");
    }

    private static void EnsureAccountTables()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
                UserName TEXT NOT NULL DEFAULT 'local',
                LoginName TEXT NOT NULL DEFAULT 'local',
                Email TEXT NOT NULL DEFAULT '',
                DisplayName TEXT NOT NULL DEFAULT 'Local User',
                PasswordHash TEXT NOT NULL DEFAULT '',
                PasswordSalt TEXT NOT NULL DEFAULT '',
                IsActive INTEGER NOT NULL DEFAULT 1,
                IsDeveloperAccount INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000Z',
                UpdatedAt TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000Z'
            );
            """);

        AddColumnIfMissing(connection, "Users", "LoginName", "TEXT NOT NULL DEFAULT 'local'");
        AddColumnIfMissing(connection, "Users", "DisplayName", "TEXT NOT NULL DEFAULT 'Local User'");
        AddColumnIfMissing(connection, "Users", "PasswordHash", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Users", "PasswordSalt", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Users", "IsActive", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "Users", "IsDeveloperAccount", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "Users", "CreatedAt", "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000Z'");
        AddColumnIfMissing(connection, "Users", "UpdatedAt", "TEXT NOT NULL DEFAULT '0001-01-01T00:00:00.0000000Z'");

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS AccountSubscriptions (
                Id TEXT NOT NULL CONSTRAINT PK_AccountSubscriptions PRIMARY KEY,
                UserId TEXT NOT NULL,
                PlanType INTEGER NOT NULL DEFAULT 0,
                Status INTEGER NOT NULL DEFAULT 0,
                StartedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CurrentPeriodEndsAt TEXT NULL,
                TrialEndsAt TEXT NULL,
                ExternalCustomerId TEXT NOT NULL DEFAULT '',
                ExternalSubscriptionId TEXT NOT NULL DEFAULT '',
                EntitlementsOverrideSerialized TEXT NOT NULL DEFAULT ''
            );
            """);

        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_Users_Email ON Users (Email);");
        ExecuteNonQuery(connection, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_LoginName ON Users (LoginName);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_AccountSubscriptions_UserId ON AccountSubscriptions (UserId);");
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

    private static void EnsureActivityTables()
    {
        if (!File.Exists(ResolvedDatabasePath))
        {
            return;
        }

        using var connection = new SqliteConnection($"Data Source={ResolvedDatabasePath}");
        connection.Open();

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS ActivityEvents (
                Id TEXT NOT NULL CONSTRAINT PK_ActivityEvents PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                WorkItemId TEXT NULL,
                Timestamp TEXT NOT NULL,
                EventType INTEGER NOT NULL,
                AppName TEXT NOT NULL DEFAULT '',
                ProcessName TEXT NOT NULL DEFAULT '',
                WindowTitle TEXT NOT NULL DEFAULT '',
                FilePath TEXT NULL,
                FolderPath TEXT NULL,
                DurationSeconds INTEGER NULL,
                Value REAL NULL,
                MetadataJson TEXT NOT NULL DEFAULT '{}'
            );
            """);

        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_ActivityEvents_ProjectId_Timestamp ON ActivityEvents (ProjectId, Timestamp);");
        ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS IX_ActivityEvents_ProjectId_EventType_Timestamp ON ActivityEvents (ProjectId, EventType, Timestamp);");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        if (ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        ExecuteNonQuery(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};");
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
