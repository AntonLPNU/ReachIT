using System.IO;
using System.IO.Compression;
using System.Text;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class DeveloperProjectGeneratorService : IDeveloperProjectGeneratorService
{
    private static readonly string[] RandomFolders =
    [
        "DevTest/Notes",
        "DevTest/Code",
        "DevTest/Data",
        "DevTest/Design",
        "DevTest/Reports",
        "DevTest/Client",
        "DevTest/Archive"
    ];

    private static readonly string[] RandomExtensions =
    [
        ".txt", ".docx", ".odt", ".rtf", ".md", ".tex",
        ".xlsx", ".xls", ".csv", ".ods",
        ".pptx", ".ppt", ".odp",
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".bmp", ".ico",
        ".mp3", ".wav", ".ogg", ".flac", ".m4a",
        ".mp4", ".mov", ".avi", ".mkv", ".webm",
        ".zip", ".rar", ".7z", ".tar", ".gz",
        ".html", ".css", ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".cs", ".cpp", ".c", ".h",
        ".json", ".xml", ".yaml", ".yml", ".sql", ".db", ".sqlite", ".sqlite3", ".parquet", ".log",
        ".psd", ".ai", ".fig", ".blend", ".fbx", ".obj", ".stl", ".glb", ".gltf",
        ".env", ".ini", ".toml", ".config", ".lock", ".gitignore", ".editorconfig",
        ".exe", ".msi", ".apk", ".app", ".bat", ".sh"
    ];

    private readonly IProjectService _projectService;
    private readonly IDialogService _dialogService;
    private readonly ITaskService _taskService;

    public DeveloperProjectGeneratorService(
        IProjectService projectService,
        IDialogService dialogService,
        ITaskService taskService)
    {
        _projectService = projectService;
        _dialogService = dialogService;
        _taskService = taskService;
    }

    public async Task<ProjectMeta?> GenerateDemoProjectAsync(CancellationToken cancellationToken = default)
    {
        var root = _dialogService.ShowOpenFolderDialog();
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var project = await _projectService.CreateProjectAsync(new CreateProjectRequest
        {
            ProjectName = $"ReachIT Demo Lab {DateTime.Now:yyyyMMdd-HHmm}",
            Description = "Developer-generated project for testing ReachIT navigation, activity tracking, tasks, files, and project structure.",
            SaveLocation = root,
            TemplateType = ProjectTemplateType.ResearchProject,
            FinalGoal = "Test every major ReachIT workflow with realistic mixed project materials.",
            MainTopic = "Launching a small productivity product",
            DesiredResult = "A complete simulated workspace with plans, notes, research, design, code, assets, reports, and client-style documents.",
            ResultFormat = "ReachIT demo project",
            KnownSections = "Market research;Product planning;Design system;Prototype;Documentation;Release checklist;Client feedback;Archive",
            DetailLevel = "Detailed",
            DeadlineDate = DateTime.Today.AddDays(21),
            CreateStarterFiles = true,
            LinkTasksToFiles = true
        }, cancellationToken).ConfigureAwait(false);

        await WriteDemoFilesAsync(project.ProjectDirectoryPath, cancellationToken).ConfigureAwait(false);
        await GenerateFormatCatalogAsync(project, cancellationToken).ConfigureAwait(false);
        return project;
    }

    public async Task<IReadOnlyList<string>> GenerateRandomFilesAsync(ProjectMeta project, int count, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 200);
        var created = new List<string>();

        for (var index = 0; index < count; index++)
        {
            var folder = RandomFolders[Random.Shared.Next(RandomFolders.Length)];
            var extension = RandomExtensions[Random.Shared.Next(RandomExtensions.Length)];
            var fileName = $"{DateTime.Now:HHmmss}-{Guid.NewGuid():N}"[..24] + extension;
            var relativePath = Path.Combine(folder, fileName);
            var fullPath = Path.Combine(project.ProjectDirectoryPath, relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, BuildRandomContent(relativePath, extension), cancellationToken).ConfigureAwait(false);
            if (extension is ".docx" or ".xlsx" or ".pptx" or ".odt" or ".ods" or ".odp" or ".zip")
            {
                CreateStructuredSample(fullPath, extension);
            }
            created.Add(fullPath);
        }

        await CreateTaskHierarchyForRandomFilesAsync(project, created, cancellationToken).ConfigureAwait(false);
        return created;
    }

    public async Task<int> GenerateTestTasksAsync(ProjectMeta project, int count, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 100);
        var rootTasks = new[]
        {
            "Plan project structure",
            "Prepare source materials",
            "Build first working version",
            "Review quality",
            "Package final result"
        };

        var created = 0;
        foreach (var rootTitle in rootTasks)
        {
            if (created >= count)
            {
                break;
            }

            var root = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Test backlog: {rootTitle}",
                Description = "Developer-generated task used to test planning, filtering, deadlines, nesting, and dashboard counters.",
                Priority = Random.Shared.Next(1, 4),
                Status = created % 2 == 0 ? "In Progress" : "To Do",
                DueDateUtc = DateTime.UtcNow.AddDays(Random.Shared.Next(1, 14))
            };
            await _taskService.AddTaskAsync(root, cancellationToken).ConfigureAwait(false);
            created++;

            foreach (var childTitle in TestChildTasks(rootTitle).Take(Math.Max(0, count - created)))
            {
                var child = new TaskItem
                {
                    Id = Guid.NewGuid(),
                    Title = childTitle,
                    Description = $"Generated child task under '{rootTitle}'.",
                    ParentTaskId = root.Id,
                    Priority = Random.Shared.Next(1, 4),
                    Status = "To Do",
                    DueDateUtc = DateTime.UtcNow.AddDays(Random.Shared.Next(1, 21))
                };
                await _taskService.AddTaskAsync(child, cancellationToken).ConfigureAwait(false);
                created++;

                if (created >= count)
                {
                    break;
                }
            }
        }

        return created;
    }

    public async Task<IReadOnlyList<string>> GenerateFormatCatalogAsync(ProjectMeta project, CancellationToken cancellationToken = default)
    {
        var created = new List<string>();
        foreach (var extension in RandomExtensions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var folder = GetFormatFolder(extension);
            var fileName = extension is ".gitignore" or ".editorconfig"
                ? extension
                : $"sample{extension}";
            var fullPath = Path.Combine(project.ProjectDirectoryPath, "FormatCatalog", folder, fileName);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (extension is ".docx" or ".xlsx" or ".pptx" or ".odt" or ".ods" or ".odp" or ".zip")
            {
                CreateStructuredSample(fullPath, extension);
            }
            else if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico")
            {
                WriteTinyImagePlaceholder(fullPath, extension);
            }
            else
            {
                await File.WriteAllTextAsync(fullPath, BuildRandomContent(Path.GetRelativePath(project.ProjectDirectoryPath, fullPath), extension), cancellationToken).ConfigureAwait(false);
            }

            created.Add(fullPath);
        }

        return created;
    }

    public async Task<IReadOnlyList<string>> TouchRandomFilesAsync(ProjectMeta project, int count, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 200);
        var files = GetProjectFiles(project).ToList();
        if (files.Count == 0)
        {
            return [];
        }

        var touched = new List<string>();
        foreach (var file in files.OrderBy(_ => Random.Shared.Next()).Take(count))
        {
            await File.AppendAllTextAsync(file, $"{Environment.NewLine}Dev test touch: {DateTime.Now:O}", cancellationToken).ConfigureAwait(false);
            touched.Add(file);
        }

        return touched;
    }

    public string? PickRandomProjectFile(ProjectMeta project)
    {
        var files = GetProjectFiles(project).ToList();
        return files.Count == 0 ? null : files[Random.Shared.Next(files.Count)];
    }

    private static async Task WriteDemoFilesAsync(string projectDirectory, CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>
        {
            ["README.md"] = "# ReachIT Demo Lab\n\nThis project is intentionally noisy: plans, notes, code, assets, research, reports, and checklists all live together so ReachIT features have something meaningful to inspect.\n",
            ["Planning/roadmap.md"] = "# Roadmap\n\n## Phase 1\n- Validate the user problem\n- Draft the core workflow\n- Create first prototype\n\n## Phase 2\n- Track activity\n- Link tasks to files\n- Prepare release notes\n",
            ["Planning/decision-log.md"] = "# Decision log\n\n| Date | Decision | Reason |\n| --- | --- | --- |\n| Today | Start with local-first data | Faster prototype and easier testing |\n| Today | Keep subscription checks isolated | Future billing should not leak into core logic |\n",
            ["Research/market-notes.md"] = "# Market notes\n\nUsers want a tool that turns messy project folders into structured work. The biggest risk is feature overload without guidance.\n",
            ["Research/interview-summary.md"] = "# Interview summary\n\n## Student\nNeeds deadlines, simple task splits, and file reminders.\n\n## Freelancer\nNeeds client files, revisions, notes, and delivery checklists in one place.\n\n## Maker\nNeeds drafts, references, and exports to stay connected.\n",
            ["Research/competitors.csv"] = "Name,Strength,Risk\nGenericTodo,Simple tasks,No file context\nProjectSuite,Powerful,Too heavy\nFolderNotes,Flexible,No progress model\n",
            ["Product/problem-statement.md"] = "# Problem statement\n\nProject folders collect useful files but lose intent. ReachIT should keep goal, tasks, files, and progress connected.\n",
            ["Product/personas.md"] = "# Personas\n\n## Student Sasha\nWants study projects to feel smaller.\n\n## Freelancer Mira\nWants client work to stay organized.\n\n## Developer Artem\nWants test data with many file types.\n",
            ["Product/requirements.md"] = "# Requirements\n\n- Create and open folder-based projects\n- Link tasks to files\n- Track useful activity\n- Keep settings understandable\n- Gate future premium features through entitlements\n",
            ["Design/wireframe-notes.md"] = "# Wireframe notes\n\nMain dashboard should show goal, current task, recent files, and progress. Settings should expose account state without making the app feel like billing software.\n",
            ["Design/style-tokens.json"] = "{\n  \"primary\": \"#4361EE\",\n  \"accent\": \"#4CC9F0\",\n  \"danger\": \"#F72585\",\n  \"surface\": \"#151E31\"\n}\n",
            ["Code/sample-api.cs"] = "namespace Demo;\n\npublic interface IProjectInsightService\n{\n    Task<string> SummarizeAsync(string projectPath, CancellationToken cancellationToken = default);\n}\n",
            ["Code/sample-config.json"] = "{\n  \"activityTracking\": true,\n  \"taskSuggestions\": true,\n  \"subscriptionPlan\": \"Internal\"\n}\n",
            ["Code/scripts/build-demo.ps1"] = "Write-Host \"Building demo workspace...\"\nWrite-Host \"This script is only sample content for ReachIT testing.\"\n",
            ["Client/client-brief.md"] = "# Client brief\n\nCreate a clear workflow for a small product launch. Include research, design, implementation notes, and release tasks.\n",
            ["Client/feedback-round-1.md"] = "# Feedback round 1\n\n- Make the first screen more useful\n- Add clearer status messages\n- Explain what premium unlocks later\n",
            ["Reports/weekly-report.md"] = "# Weekly report\n\n## Done\n- Project structure created\n- Research notes drafted\n- Subscription architecture isolated\n\n## Next\n- Add real login flow\n- Connect billing provider\n",
            ["Reports/risks.md"] = "# Risks\n\n- Too many features without onboarding\n- Activity tracking needs clear privacy controls\n- Subscription gates must not break core offline use\n",
            ["Assets/logo-notes.txt"] = "Logo usage notes: use AppIcon.png for app surfaces, splash screens, and compact launch states.",
            ["Assets/image-prompts.md"] = "# Image prompts\n\n- Dashboard screenshot mockup\n- Clean project folder illustration\n- Focus mode visual state\n",
            ["Tasks/manual-test-checklist.md"] = "# Manual test checklist\n\n- Open project\n- Browse tree\n- Create task\n- Link file\n- Start focus mode\n- Generate activity\n- View dashboard\n- Save settings\n",
            ["Tasks/bug-bash.md"] = "# Bug bash\n\nTry long filenames, empty folders, repeated task names, many recent files, and nested directories.\n",
            ["Archive/old-plan.md"] = "# Old plan\n\nThis file exists to test archive folders and older project documents.\n",
            ["Archive/release-v0-notes.md"] = "# Release v0 notes\n\nPrototype release for internal testing only.\n"
        };

        foreach (var (relativePath, content) in files)
        {
            var path = Path.Combine(projectDirectory, relativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        }

        CreateBinaryLikePlaceholders(projectDirectory);
    }

    private static void CreateBinaryLikePlaceholders(string projectDirectory)
    {
        var paths = new[]
        {
            "Assets/mockup-placeholder.png.txt",
            "Exports/presentation-placeholder.pdf.txt",
            "Exports/demo-build.zip.txt",
            "Materials/imported-client-file.docx.txt"
        };

        foreach (var relativePath in paths)
        {
            var path = Path.Combine(projectDirectory, relativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, "Placeholder for an external/binary-style file used during ReachIT testing.");
        }
    }

    private static IEnumerable<string> GetProjectFiles(ProjectMeta project)
    {
        if (string.IsNullOrWhiteSpace(project.ProjectDirectoryPath) || !Directory.Exists(project.ProjectDirectoryPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(project.ProjectDirectoryPath, "*.*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> TestChildTasks(string rootTitle)
    {
        return rootTitle switch
        {
            "Plan project structure" =>
            [
                "Write final goal",
                "Split work into stages",
                "Choose first small task",
                "Check deadline risk"
            ],
            "Prepare source materials" =>
            [
                "Collect references",
                "Sort files by folder",
                "Mark missing assets",
                "Attach important documents"
            ],
            "Build first working version" =>
            [
                "Create draft result",
                "Test core workflow",
                "Record blockers",
                "Update progress notes"
            ],
            "Review quality" =>
            [
                "Review open tasks",
                "Fix naming issues",
                "Validate linked files",
                "Mark completed items"
            ],
            _ =>
            [
                "Prepare archive",
                "Write release notes",
                "Export final files",
                "Close project checklist"
            ]
        };
    }

    private async Task CreateTaskHierarchyForRandomFilesAsync(
        ProjectMeta project,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        var batchTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = $"Developer random file batch {DateTime.Now:yyyy-MM-dd HH:mm}",
            Description = "Root task for a generated developer-test file batch. Every child task is linked to generated files so hierarchy, file attachments, dashboards, and activity reactions can be tested together.",
            Priority = 3,
            Status = "In Progress",
            DueDateUtc = DateTime.UtcNow.AddDays(7)
        };
        await _taskService.AddTaskAsync(batchTask, cancellationToken).ConfigureAwait(false);

        foreach (var folderGroup in files.GroupBy(path => GetTaskArea(project, path)).OrderBy(group => group.Key))
        {
            var areaTask = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Area: {folderGroup.Key}",
                Description = $"Container for generated files in {folderGroup.Key}.",
                ParentTaskId = batchTask.Id,
                Priority = 2,
                Status = "In Progress",
                DueDateUtc = DateTime.UtcNow.AddDays(5)
            };
            await _taskService.AddTaskAsync(areaTask, cancellationToken).ConfigureAwait(false);

            foreach (var file in folderGroup.OrderBy(Path.GetFileName))
            {
                await CreateFileTaskBranchAsync(project, areaTask.Id, file, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task CreateFileTaskBranchAsync(
        ProjectMeta project,
        Guid parentTaskId,
        string filePath,
        CancellationToken cancellationToken)
    {
        var relativePath = Path.GetRelativePath(project.ProjectDirectoryPath, filePath);
        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var normalizedPath = NormalizePath(filePath);

        var fileTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = $"Process file: {fileName}",
            Description = $"Generated file task for {relativePath}. Use this branch to test task nesting, file links, and activity suggestions.",
            ParentTaskId = parentTaskId,
            AttachedFilePath = normalizedPath,
            Priority = GetPriorityForExtension(extension),
            Status = "To Do",
            DueDateUtc = DateTime.UtcNow.AddDays(Random.Shared.Next(1, 10))
        };
        await _taskService.AddTaskAsync(fileTask, cancellationToken).ConfigureAwait(false);

        var subtasks = new[]
        {
            new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Inspect content: {fileName}",
                Description = $"Open or preview {relativePath} and verify ReachIT can show useful information for {extension}.",
                ParentTaskId = fileTask.Id,
                AttachedFilePath = normalizedPath,
                Priority = fileTask.Priority,
                Status = "To Do",
                DueDateUtc = DateTime.UtcNow.AddDays(1)
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Update metadata: {fileName}",
                Description = "Check title, folder, recent-file state, activity timeline, and linked-task visibility.",
                ParentTaskId = fileTask.Id,
                AttachedFilePath = normalizedPath,
                Priority = 2,
                Status = "To Do",
                DueDateUtc = DateTime.UtcNow.AddDays(2)
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"Verify workflow reaction: {fileName}",
                Description = "Touch or open the file and confirm dashboards, suggestions, and activity tracking react as expected.",
                ParentTaskId = fileTask.Id,
                AttachedFilePath = normalizedPath,
                Priority = 3,
                Status = "To Do",
                DueDateUtc = DateTime.UtcNow.AddDays(3)
            }
        };

        foreach (var subtask in subtasks)
        {
            await _taskService.AddTaskAsync(subtask, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string GetTaskArea(ProjectMeta project, string filePath)
    {
        var relativePath = Path.GetRelativePath(project.ProjectDirectoryPath, filePath);
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length >= 2 && parts[0].Equals("DevTest", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : parts.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Generated";
    }

    private static int GetPriorityForExtension(string extension)
    {
        return extension switch
        {
            ".exe" or ".msi" or ".apk" or ".app" or ".bat" or ".sh" => 3,
            ".db" or ".sqlite" or ".sqlite3" or ".json" or ".xml" or ".sql" => 3,
            ".cs" or ".py" or ".js" or ".ts" or ".tsx" or ".jsx" or ".java" or ".cpp" or ".c" or ".h" => 2,
            _ => 1
        };
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static string BuildRandomContent(string relativePath, string extension)
    {
        var title = Path.GetFileNameWithoutExtension(relativePath);
        return extension switch
        {
            ".json" => $$"""
                {
                  "id": "{{Guid.NewGuid():N}}",
                  "kind": "developer-test",
                  "path": "{{relativePath.Replace("\\", "\\\\")}}",
                  "createdAt": "{{DateTime.UtcNow:O}}",
                  "score": {{Random.Shared.Next(1, 100)}}
                }
                """,
            ".csv" => "Key,Value,Note\n" +
                      $"Created,{DateTime.UtcNow:O},Developer random file\n" +
                      $"Signal,{Random.Shared.Next(1, 100)},Used for table/file tests\n",
            ".cs" => $"namespace ReachIT.DevTest;\n\npublic sealed class {SanitizeIdentifier(title)}\n{{\n    public string Name => \"{title}\";\n}}\n",
            ".ps1" => $"Write-Host \"ReachIT dev test file: {title}\"\nWrite-Host \"Created {DateTime.UtcNow:O}\"\n",
            ".html" => $"<!doctype html>\n<title>{title}</title>\n<h1>{title}</h1>\n<p>Generated by ReachIT developer test panel.</p>\n",
            ".rtf" => "{\\rtf1\\ansi\\b ReachIT RTF sample\\b0\\par This file tests formatted text preview.}",
            ".xml" => $"<sample><path>{System.Security.SecurityElement.Escape(relativePath)}</path><created>{DateTime.UtcNow:O}</created></sample>",
            ".yaml" or ".yml" => $"kind: developer-test\npath: {relativePath}\ncreatedAt: {DateTime.UtcNow:O}\n",
            ".sql" => "CREATE TABLE reachit_demo (id INTEGER PRIMARY KEY, title TEXT);\nINSERT INTO reachit_demo(title) VALUES ('Developer test');\n",
            ".css" => "body { font-family: Segoe UI, sans-serif; color: #eaf0ff; background: #101827; }\n",
            ".js" or ".ts" => "export const reachItDevSample = 'Generated for file support tests';\n",
            ".jsx" or ".tsx" => "export function Sample(){ return <div>ReachIT developer sample</div>; }\n",
            ".py" => "print('ReachIT developer sample')\n",
            ".java" => "public class ReachItSample { public static void main(String[] args) { System.out.println(\"ReachIT\"); } }\n",
            ".cpp" or ".c" or ".h" => "#include <stdio.h>\nint main(){ printf(\"ReachIT developer sample\\n\"); return 0; }\n",
            ".tex" => "\\section{ReachIT Sample}\nThis is a LaTeX test file.\n",
            ".env" => "REACHIT_MODE=developer\nREACHIT_SAMPLE=true\n",
            ".ini" => "[reachit]\nmode=developer\nsample=true\n",
            ".toml" => "[reachit]\nmode = \"developer\"\nsample = true\n",
            ".config" => "<configuration><reachit mode=\"developer\" /></configuration>\n",
            ".log" => $"{DateTime.UtcNow:O} INFO ReachIT developer sample log line\n",
            ".obj" => "o ReachITCube\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n",
            ".gltf" => "{\"asset\":{\"version\":\"2.0\",\"generator\":\"ReachIT\"},\"scene\":0,\"scenes\":[{}]}\n",
            _ => $"# {title}\n\nGenerated by ReachIT developer test panel.\n\n- Path: {relativePath}\n- Created: {DateTime.UtcNow:O}\n- Purpose: test file tree, activity tracking, task suggestions, and recent activity.\n"
        };
    }

    private static string GetFormatFolder(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".txt" or ".docx" or ".odt" or ".rtf" or ".md" or ".tex" => "TextDocuments",
            ".xlsx" or ".xls" or ".csv" or ".ods" => "Spreadsheets",
            ".pptx" or ".ppt" or ".odp" => "Presentations",
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".svg" or ".bmp" or ".ico" => "Images",
            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a" => "Audio",
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" => "Video",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archives",
            ".html" or ".css" or ".js" or ".ts" or ".jsx" or ".tsx" or ".py" or ".java" or ".cs" or ".cpp" or ".c" or ".h" => "Code",
            ".db" or ".sqlite" or ".sqlite3" or ".json" or ".xml" or ".yaml" or ".yml" or ".sql" or ".parquet" or ".log" => "Data",
            ".psd" or ".ai" or ".fig" or ".blend" or ".fbx" or ".obj" or ".stl" or ".glb" or ".gltf" => "Design3D",
            ".env" or ".ini" or ".toml" or ".config" or ".lock" or ".gitignore" or ".editorconfig" => "ProjectConfig",
            _ => "Executables"
        };
    }

    private static void CreateStructuredSample(string fullPath, string extension)
    {
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        using var archive = ZipFile.Open(fullPath, ZipArchiveMode.Create);
        switch (extension.ToLowerInvariant())
        {
            case ".docx":
                AddZipText(archive, "word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>ReachIT Word document sample with readable text.</w:t></w:r></w:p></w:body></w:document>");
                break;
            case ".xlsx":
                AddZipText(archive, "xl/sharedStrings.xml", "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>ReachIT Excel shared string</t></si><si><t>42</t></si></sst>");
                break;
            case ".pptx":
                AddZipText(archive, "ppt/slides/slide1.xml", "<p:sld xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"><p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>ReachIT PowerPoint slide sample</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld></p:sld>");
                break;
            case ".odt":
            case ".ods":
            case ".odp":
                AddZipText(archive, "content.xml", "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"><office:body><text:p>ReachIT OpenDocument sample text.</text:p></office:body></office:document-content>");
                break;
            default:
                AddZipText(archive, "README.txt", "ReachIT archive sample.");
                break;
        }
    }

    private static void AddZipText(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static void WriteTinyImagePlaceholder(string fullPath, string extension)
    {
        if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(fullPath, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"120\" height=\"80\"><rect width=\"120\" height=\"80\" fill=\"#4361ee\"/><text x=\"10\" y=\"45\" fill=\"white\">ReachIT</text></svg>");
            return;
        }

        File.WriteAllBytes(fullPath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));
    }

    private static string SanitizeIdentifier(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        var result = chars.Length == 0 ? "GeneratedFile" : new string(chars);
        return char.IsDigit(result[0]) ? $"Generated{result}" : result;
    }
}
