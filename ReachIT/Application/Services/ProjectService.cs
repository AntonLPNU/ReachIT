// Handles project create/open and .rit metadata persistence for folder-based projects.
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class ProjectService : IProjectService
{
    private readonly IDialogService _dialogService;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IDatabaseService _databaseService;
    private readonly IFileSystemProjectExplorerService _fileSystemProjectExplorerService;
    private readonly IWorkItemRepository _workItemRepository;
    private readonly ITaskService _taskService;

    public ProjectService(
        IDialogService dialogService,
        IRecentFilesService recentFilesService,
        IDatabaseService databaseService,
        IFileSystemProjectExplorerService fileSystemProjectExplorerService,
        IWorkItemRepository workItemRepository,
        ITaskService taskService)
    {
        _dialogService = dialogService;
        _recentFilesService = recentFilesService;
        _databaseService = databaseService;
        _fileSystemProjectExplorerService = fileSystemProjectExplorerService;
        _workItemRepository = workItemRepository;
        _taskService = taskService;
    }

    public ProjectMeta? CurrentProject { get; private set; }

    public async Task<ProjectMeta> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("Project name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SaveLocation))
        {
            throw new ArgumentException("Save location is required.", nameof(request));
        }

        var safeProjectName = string.Join("_", request.ProjectName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeProjectName))
        {
            safeProjectName = "ReachITProject";
        }

        var projectDirectory = Path.Combine(request.SaveLocation, safeProjectName);
        Directory.CreateDirectory(projectDirectory);
        CreateTemplateStructure(projectDirectory, request.TemplateType);

        var meta = new ProjectMeta
        {
            Id = Guid.NewGuid(),
            ProjectName = request.ProjectName,
            Description = request.Description,
            ProjectDirectoryPath = projectDirectory,
            RitFilePath = Path.Combine(projectDirectory, ".reachit.json"),
            TemplateType = request.TemplateType,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await SaveRitFileAsync(meta, cancellationToken).ConfigureAwait(false);
        await UpsertProjectMetaAsync(meta, cancellationToken).ConfigureAwait(false);
        await CreateTemplatePlanAsync(meta, request, cancellationToken).ConfigureAwait(false);

        CurrentProject = meta;
        await _recentFilesService.AddRecentProjectAsync(meta, cancellationToken).ConfigureAwait(false);

        foreach (var externalPath in request.InitialExternalFiles)
        {
            await _recentFilesService.AddRecentExternalFileAsync(new RecentExternalFileItem
            {
                DisplayName = Path.GetFileName(externalPath),
                SourcePathOrUrl = externalPath,
                LastOpenedAtUtc = DateTime.UtcNow
            }, cancellationToken).ConfigureAwait(false);
        }

        return meta;
    }

    private static void CreateTemplateStructure(string projectDirectory, ProjectTemplateType templateType)
    {
        IReadOnlyList<string> folders = templateType switch
        {
            ProjectTemplateType.StudyProject => ["Planning", "Notes", "Materials", "Reports", "Tasks", "Sources"],
            ProjectTemplateType.FreelanceProject => ["Planning", "Client", "Files", "Deliverables", "Tasks", "Versions", "References"],
            ProjectTemplateType.CreativeProject => ["Planning", "Assets", "References", "Exports", "Versions", "Notes"],
            ProjectTemplateType.ResearchProject => ["Planning", "Sources", "Notes", "Links", "Drafts", "Results"],
            _ => ["Planning"]
        };

        foreach (var folder in folders)
        {
            Directory.CreateDirectory(Path.Combine(projectDirectory, folder));
        }
    }

    private async Task CreateTemplatePlanAsync(ProjectMeta project, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var plan = BuildTemplatePlan(project, request);

        if (request.CreateStarterFiles)
        {
            foreach (var file in plan.StarterFiles)
            {
                await WriteStarterFileAsync(project.ProjectDirectoryPath, file.RelativePath, file.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var item in plan.Items)
        {
            await AddWorkItemTreeAsync(project.Id, project.ProjectDirectoryPath, item, null, request.LinkTasksToFiles, cancellationToken).ConfigureAwait(false);
        }

        await CreateLegacyStarterTasksAsync(plan, request, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddWorkItemTreeAsync(
        Guid projectId,
        string projectDirectory,
        TemplatePlanNode node,
        Guid? parentId,
        bool linkTasksToFiles,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var item = new WorkItem
        {
            Id = node.Id,
            ProjectId = projectId,
            ParentId = parentId,
            Title = node.Title,
            Description = node.Description,
            Type = node.Type,
            Status = node.Status,
            Priority = node.Priority,
            ProgressPercent = node.Status == WorkItemStatus.Done ? 100 : 0,
            CreatedAt = now,
            UpdatedAt = now,
            Deadline = node.Deadline,
            EstimatedWorkUnits = node.EstimatedWorkUnits,
            LinkedPath = linkTasksToFiles ? NormalizeProjectPath(projectDirectory, node.LinkedPath) : string.Empty,
            Tags = node.Tags,
            Notes = node.Notes
        };

        await _workItemRepository.AddAsync(item, cancellationToken).ConfigureAwait(false);

        foreach (var child in node.Children)
        {
            await AddWorkItemTreeAsync(projectId, projectDirectory, child, item.Id, linkTasksToFiles, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CreateLegacyStarterTasksAsync(TemplateProjectPlan plan, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var starterTasks = plan.LegacyTasks.Take(IsDetailed(request) ? 8 : 5).ToList();
        foreach (var task in starterTasks)
        {
            if (!request.LinkTasksToFiles)
            {
                task.AttachedFilePath = null;
            }

            await _taskService.AddTaskAsync(task, cancellationToken).ConfigureAwait(false);
        }
    }

    private static TemplateProjectPlan BuildTemplatePlan(ProjectMeta project, CreateProjectRequest request)
    {
        var deadline = request.DeadlineDate.HasValue
            ? DateTime.SpecifyKind(request.DeadlineDate.Value.Date.AddHours(23).AddMinutes(59), DateTimeKind.Local).ToUniversalTime()
            : (DateTime?)null;
        var knownSections = ParseKnownSections(request.KnownSections);
        var finalGoal = FirstNonEmpty(request.FinalGoal, DefaultFinalGoal(request.TemplateType, request.MainTopic, request.DesiredResult));
        var mainTopic = FirstNonEmpty(request.MainTopic, request.ProjectName);
        var desiredResult = FirstNonEmpty(request.DesiredResult, request.ResultFormat, "A clear finished result that can be used, submitted, shown, or archived.");
        var nextStep = DefaultNextStep(request.TemplateType);
        var files = StarterFilesFor(request.TemplateType, finalGoal, mainTopic, desiredResult);
        var root = Node(
            $"Final goal: {finalGoal}",
            $"Main direction: {mainTopic}\nDesired result: {desiredResult}\nRecommended next step: {nextStep}",
            WorkItemType.Goal,
            WorkItemStatus.InProgress,
            3,
            deadline: deadline,
            tags: "template;goal");

        root.Children.Add(Node(
            $"Start here: {nextStep}",
            "This is the first small action. Complete it before trying to organize the whole project.",
            WorkItemType.Task,
            WorkItemStatus.InProgress,
            3,
            StarterLink(request.TemplateType, files),
            deadline,
            "next-step;template"));

        foreach (var stage in StagesFor(request.TemplateType, mainTopic, knownSections, request))
        {
            root.Children.Add(stage);
        }

        return new TemplateProjectPlan
        {
            StarterFiles = request.CreateStarterFiles ? files : [],
            Items = [root],
            LegacyTasks = BuildLegacyTasks(root, project.ProjectDirectoryPath)
        };
    }

    private static IReadOnlyList<TemplatePlanNode> StagesFor(
        ProjectTemplateType templateType,
        string mainTopic,
        IReadOnlyList<string> knownSections,
        CreateProjectRequest request)
    {
        return templateType switch
        {
            ProjectTemplateType.StudyProject => StudyStages(mainTopic, knownSections, request),
            ProjectTemplateType.FreelanceProject => FreelanceStages(knownSections, request),
            ProjectTemplateType.CreativeProject => CreativeStages(knownSections, request),
            ProjectTemplateType.ResearchProject => ResearchStages(knownSections, request),
            _ => EmptyStages(request)
        };
    }

    private static IReadOnlyList<TemplatePlanNode> EmptyStages(CreateProjectRequest request)
    {
        var plan = Stage("Shape the project", "Turn the empty project into a usable plan.", "Planning/project-plan.md", request,
        [
            TemplateTask("Describe what should exist at the end", "Write the expected result in plain language.", "Planning/project-plan.md"),
            TemplateTask("Choose the first small action", "Pick one task that can be done in 10-30 minutes.", "Planning/project-plan.md"),
            TemplateTask("Create required files or folders", "Add only the materials the project actually needs.", "Planning"),
            TemplateTask("Split the project into stages", "Create rough stages before adding many detailed tasks.", "Planning/project-plan.md")
        ]);

        return [plan];
    }

    private static IReadOnlyList<TemplatePlanNode> StudyStages(string mainTopic, IReadOnlyList<string> knownSections, CreateProjectRequest request)
    {
        var stages = new List<TemplatePlanNode>
        {
            Stage("Understand requirements", "Clarify what exactly must be learned, submitted, or demonstrated.", "Notes/requirements.md", request,
            [
                TemplateTask("Read the assignment or topic description", "Extract the concrete requirement from the source material.", "Notes/requirements.md"),
                TemplateTask("Write what must be submitted", "List the final document, presentation, lab result, test, or proof of understanding.", "Notes/requirements.md"),
                TemplateTask("Define evaluation criteria", "Record what will make this work good enough.", "Notes/requirements.md"),
                TemplateTask("Confirm the deadline", "Add the real deadline or leave it open if the project is self-paced.", "Notes/requirements.md")
            ]),
            Stage("Break the topic into subtopics", "Turn the broad topic into manageable learning blocks.", "Notes/topic-map.md", request,
            [
                TemplateTask("List key concepts", $"Write the main concepts inside {mainTopic}.", "Notes/topic-map.md"),
                TemplateTask("Mark difficult subtopics", "Highlight what needs more time or better sources.", "Notes/topic-map.md"),
                TemplateTask("Set priorities", "Choose what must be understood first.", "Notes/topic-map.md")
            ]),
            Stage("Collect materials", "Gather sources, links, files, examples, and references.", "Sources/sources.md", request,
            [
                TemplateTask("Find sources", "Collect books, articles, videos, lecture notes, or examples.", "Sources/sources.md"),
                TemplateTask("Save links and files", "Attach or copy useful materials into the project.", "Sources/sources.md"),
                TemplateTask("Make first notes", "Write a rough summary before polishing anything.", "Notes")
            ]),
            Stage("Do the main work", "Write, solve, prepare, build, or practice the central result.", "Reports/final-result.md", request,
            [
                TemplateTask("Create the first working version", "Start with a draft, not a perfect final result.", "Reports/final-result.md"),
                TemplateTask("Add examples or proof", "Support the answer with examples, calculations, citations, or screenshots.", "Reports/final-result.md"),
                TemplateTask("Check completeness", "Compare the work against the requirements.", "Notes/requirements.md")
            ]),
            Stage("Review and submit", "Turn the work into a clean final version.", "Reports/final-result.md", request,
            [
                TemplateTask("Fix mistakes", "Correct factual, formatting, and logic issues.", "Reports/final-result.md"),
                TemplateTask("Prepare final version", "Make the final file ready to submit or reuse.", "Reports/final-result.md"),
                TemplateTask("Mark the project complete", "Confirm the result exists and is usable.", "Reports/final-result.md")
            ])
        };

        AddKnownSectionTasks(stages[1], knownSections, "Study subtopic", "Find sources, read material, make short notes, and check understanding.", "Notes");
        return stages;
    }

    private static IReadOnlyList<TemplatePlanNode> FreelanceStages(IReadOnlyList<string> knownSections, CreateProjectRequest request)
    {
        var stages = new List<TemplatePlanNode>
        {
            Stage("Clarify the order", "Collect client requirements before starting production.", "Client/requirements.md", request,
            [
                TemplateTask("Collect client requirements", "Write what the client asked for in concrete terms.", "Client/requirements.md"),
                TemplateTask("Confirm deadline and delivery format", "Record when and how the work must be delivered.", "Client/requirements.md"),
                TemplateTask("Attach references or brief", "Keep the brief, screenshots, links, and examples close to the task.", "References/references.md"),
                TemplateTask("Write constraints", "List limits, must-haves, budget notes, or technical boundaries.", "Client/requirements.md")
            ]),
            Stage("Plan the delivery", "Split the order into versions and approval points.", "Planning/project-plan.md", request,
            [
                TemplateTask("Define the first version", "Decide what the client will see first.", "Planning/project-plan.md"),
                TemplateTask("Define what requires approval", "Mark decisions that should not be guessed.", "Planning/project-plan.md"),
                TemplateTask("List risks", "Record unclear requirements, time risks, missing files, or scope traps.", "Planning/project-plan.md")
            ]),
            Stage("Make the first version", "Create a draft or preview that can be shown.", "Deliverables/client-preview-v1.md", request,
            [
                TemplateTask("Create main files", "Set up the working files for the order.", "Files"),
                TemplateTask("Produce the first preview", "Make something reviewable before polishing too much.", "Deliverables/client-preview-v1.md"),
                TemplateTask("Check against requirements", "Compare the preview to the client brief.", "Client/requirements.md")
            ]),
            Stage("Review with client", "Collect feedback and turn it into clear changes.", "Client/feedback.md", request,
            [
                TemplateTask("Send preview to client", "Share the current result in the agreed format.", "Deliverables/client-preview-v1.md"),
                TemplateTask("Record feedback", "Write requested changes as separate tasks.", "Client/feedback.md"),
                TemplateTask("Agree final direction", "Make sure the next revision target is clear.", "Client/feedback.md")
            ]),
            Stage("Revise and deliver", "Finish, package, and close the order.", "Deliverables/final-delivery.md", request,
            [
                TemplateTask("Apply required revisions", "Do the changes that were agreed with the client.", "Client/feedback.md"),
                TemplateTask("Prepare final files", "Export, package, or document the final result.", "Deliverables/final-delivery.md"),
                TemplateTask("Deliver and close project", "Send the final result and mark the order complete.", "Deliverables/final-delivery.md")
            ])
        };

        AddKnownSectionTasks(stages[0], knownSections, "Client requirement", "Clarify, validate, and confirm this requirement with the client.", "Client/requirements.md");
        return stages;
    }

    private static IReadOnlyList<TemplatePlanNode> CreativeStages(IReadOnlyList<string> knownSections, CreateProjectRequest request)
    {
        var stages = new List<TemplatePlanNode>
        {
            Stage("Form the idea", "Make the creative idea precise enough to begin.", "Notes/project-idea.md", request,
            [
                TemplateTask("Write the idea in one sentence", "State what this project is about without extra lore.", "Notes/project-idea.md"),
                TemplateTask("Describe mood and style", "Capture genre, tone, visual style, sound, or feel.", "Notes/project-idea.md"),
                TemplateTask("Define the audience", "Write who this is for and what they should get from it.", "Notes/project-idea.md"),
                TemplateTask("Collect references", "Save images, links, examples, and inspiration.", "References")
            ]),
            Stage("Break into parts", "Find the main components and the smallest useful version.", "Notes/structure.md", request,
            [
                TemplateTask("List main components", "Write scenes, chapters, mechanics, shots, tracks, assets, or sections.", "Notes/structure.md"),
                TemplateTask("Define the minimal version", "Separate must-have work from optional polish.", "Notes/structure.md"),
                TemplateTask("Create tasks for each part", "Turn each component into an actionable task.", "Notes/structure.md")
            ]),
            Stage("Create a draft", "Make the first rough version before polishing.", "Versions/draft-v1.md", request,
            [
                TemplateTask("Make the first prototype or draft", "Create a playable, readable, viewable, or listenable first pass.", "Versions/draft-v1.md"),
                TemplateTask("Attach the first result", "Keep the draft linked to the task so progress is visible.", "Versions/draft-v1.md"),
                TemplateTask("Decide what works", "Write what should stay and what should change.", "Versions/draft-v1.md")
            ]),
            Stage("Complete core parts", "Fill the gaps and make the work feel whole.", "Versions", request,
            [
                TemplateTask("Finish key components", "Complete the parts required for the project to make sense.", "Versions"),
                TemplateTask("Remove unnecessary parts", "Cut or postpone work that does not serve the result.", "Notes/structure.md"),
                TemplateTask("Prepare near-final version", "Create the version that is ready for polish.", "Versions")
            ]),
            Stage("Polish and finish", "Test, edit, export, publish, or archive the final result.", "Exports/final-output.md", request,
            [
                TemplateTask("Polish details", "Improve quality without changing the core direction.", "Exports/final-output.md"),
                TemplateTask("Test or review", "Watch, read, play, listen, or inspect the result.", "Exports/final-output.md"),
                TemplateTask("Export final version", "Create the file or folder that represents the finished work.", "Exports/final-output.md")
            ])
        };

        AddKnownSectionTasks(stages[1], knownSections, "Creative part", "Draft, attach, review, and finish this project part.", "Versions");
        return stages;
    }

    private static IReadOnlyList<TemplatePlanNode> ResearchStages(IReadOnlyList<string> knownSections, CreateProjectRequest request)
    {
        var stages = new List<TemplatePlanNode>
        {
            Stage("Formulate the question", "Define what the research must answer.", "Notes/research-question.md", request,
            [
                TemplateTask("Describe the topic", "Write what the research is about.", "Notes/research-question.md"),
                TemplateTask("Write the main question", "Turn the topic into a question that can be answered.", "Notes/research-question.md"),
                TemplateTask("Define research boundaries", "Write what is inside and outside the research.", "Notes/research-question.md"),
                TemplateTask("Define expected result", "Choose whether this ends as a conclusion, table, report, presentation, or source list.", "Notes/research-question.md")
            ]),
            Stage("Split into directions", "Break the research into themes or comparison criteria.", "Planning/project-plan.md", request,
            [
                TemplateTask("List key subtopics", "Create the directions that need evidence.", "Planning/project-plan.md"),
                TemplateTask("Define analysis criteria", "Write how sources or options will be compared.", "Planning/project-plan.md"),
                TemplateTask("Decide required sources", "List what kind of evidence is needed.", "Planning/project-plan.md")
            ]),
            Stage("Collect sources", "Gather and describe sources before analysis.", "Sources/sources.md", request,
            [
                TemplateTask("Find sources", "Collect primary, reliable, or relevant sources.", "Sources/sources.md"),
                TemplateTask("Describe each source", "Add a short note about usefulness and reliability.", "Sources/sources.md"),
                TemplateTask("Extract facts or quotes", "Save important evidence for later analysis.", "Sources/sources.md")
            ]),
            Stage("Analyze materials", "Compare evidence and form intermediate conclusions.", "Drafts/analysis.md", request,
            [
                TemplateTask("Write key ideas", "Summarize what the sources actually say.", "Drafts/analysis.md"),
                TemplateTask("Compare viewpoints", "Look for agreements, contradictions, and gaps.", "Drafts/analysis.md"),
                TemplateTask("Create intermediate conclusions", "Write what can already be concluded.", "Drafts/analysis.md")
            ]),
            Stage("Prepare result", "Turn research into a useful final answer.", "Results/conclusion.md", request,
            [
                TemplateTask("Write final conclusion", "Answer the main question clearly.", "Results/conclusion.md"),
                TemplateTask("Add sources", "Keep sources attached to the final result.", "Sources/sources.md"),
                TemplateTask("Check logic", "Make sure conclusion follows from evidence.", "Results/conclusion.md")
            ])
        };

        AddKnownSectionTasks(stages[1], knownSections, "Research direction", "Collect sources, analyze evidence, and write a short conclusion for this direction.", "Drafts");
        return stages;
    }

    private static TemplatePlanNode Stage(string title, string description, string linkedPath, CreateProjectRequest request, IReadOnlyList<TemplatePlanNode> children)
    {
        var stage = Node(title, description, WorkItemType.Milestone, WorkItemStatus.Planned, 2, linkedPath, request.DeadlineDate, "stage;template");
        if (IsMinimal(request))
        {
            stage.Children.AddRange(children.Take(2));
        }
        else if (IsDetailed(request))
        {
            stage.Children.AddRange(children);
            foreach (var child in children.Where(x => x.Type == WorkItemType.Task).ToList())
            {
                child.Children.Add(Node("Attach or create the related file", "Make sure the task has a concrete material, file, folder, or result linked to it.", WorkItemType.Subtask, WorkItemStatus.Backlog, 1, child.LinkedPath, request.DeadlineDate, "file-link"));
                child.Children.Add(Node("Define done criteria", "Write one sentence that tells when this task is complete.", WorkItemType.Subtask, WorkItemStatus.Backlog, 1, child.LinkedPath, request.DeadlineDate, "done-criteria"));
            }
        }
        else
        {
            stage.Children.AddRange(children);
        }

        return stage;
    }

    private static TemplatePlanNode TemplateTask(string title, string description, string linkedPath)
    {
        return Node(title, description, WorkItemType.Task, WorkItemStatus.Backlog, 2, linkedPath, tags: "task;template");
    }

    private static TemplatePlanNode Node(
        string title,
        string description,
        WorkItemType type,
        WorkItemStatus status,
        int priority,
        string linkedPath = "",
        DateTime? deadline = null,
        string tags = "")
    {
        return new TemplatePlanNode
        {
            Title = title,
            Description = description,
            Type = type,
            Status = status,
            Priority = priority,
            LinkedPath = linkedPath,
            Deadline = deadline,
            Tags = tags,
            EstimatedWorkUnits = type == WorkItemType.Milestone ? 5 : 1
        };
    }

    private static void AddKnownSectionTasks(
        TemplatePlanNode parent,
        IReadOnlyList<string> knownSections,
        string titlePrefix,
        string description,
        string linkedFolder)
    {
        foreach (var section in knownSections)
        {
            parent.Children.Add(Node($"{titlePrefix}: {section}", description, WorkItemType.Task, WorkItemStatus.Backlog, 2, linkedFolder, tags: "user-section;template"));
        }
    }

    private static IReadOnlyList<StarterFile> StarterFilesFor(ProjectTemplateType templateType, string finalGoal, string mainTopic, string desiredResult)
    {
        var common = new List<StarterFile>
        {
            new("Planning/project-goal.md", $"# Project goal\n\nFinal goal: {finalGoal}\n\nMain direction: {mainTopic}\n\nDesired result: {desiredResult}\n"),
            new("Planning/project-plan.md", "# Project plan\n\n## Known stages\n\n- \n\n## Next action\n\n- \n")
        };

        common.AddRange(templateType switch
        {
            ProjectTemplateType.StudyProject =>
            [
                new StarterFile("Notes/requirements.md", "# Requirements\n\nWhat must be learned, prepared, or submitted:\n\n- \n"),
                new StarterFile("Notes/topic-map.md", "# Topic map\n\nMain topic:\n\nSubtopics:\n\n- \n"),
                new StarterFile("Sources/sources.md", "# Sources\n\n- \n"),
                new StarterFile("Reports/final-result.md", "# Final result\n\n")
            ],
            ProjectTemplateType.FreelanceProject =>
            [
                new StarterFile("Client/requirements.md", "# Client requirements\n\n- \n"),
                new StarterFile("Client/feedback.md", "# Client feedback\n\n- \n"),
                new StarterFile("References/references.md", "# References\n\n- \n"),
                new StarterFile("Deliverables/client-preview-v1.md", "# Client preview v1\n\n"),
                new StarterFile("Deliverables/final-delivery.md", "# Final delivery\n\n")
            ],
            ProjectTemplateType.CreativeProject =>
            [
                new StarterFile("Notes/project-idea.md", "# Project idea\n\nOne sentence:\n\nMood / style:\n\nAudience:\n\n"),
                new StarterFile("Notes/structure.md", "# Structure\n\nMain parts:\n\n- \n\nMinimal version:\n\n- \n"),
                new StarterFile("Versions/draft-v1.md", "# Draft v1\n\nWhat exists:\n\nWhat works:\n\nWhat needs work:\n\n"),
                new StarterFile("Exports/final-output.md", "# Final output\n\n")
            ],
            ProjectTemplateType.ResearchProject =>
            [
                new StarterFile("Notes/research-question.md", "# Research question\n\nMain question:\n\nBoundaries:\n\nExpected result:\n\n"),
                new StarterFile("Sources/sources.md", "# Sources\n\n- \n"),
                new StarterFile("Drafts/analysis.md", "# Analysis\n\n"),
                new StarterFile("Results/conclusion.md", "# Conclusion\n\n")
            ],
            _ =>
            [
                new StarterFile("Planning/project-plan.md", "# Project plan\n\nFinal goal:\n\nFirst small step:\n\nStages:\n\n- \n")
            ]
        });

        return common
            .GroupBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    private static string StarterLink(ProjectTemplateType templateType, IReadOnlyList<StarterFile> files)
    {
        var preferred = templateType switch
        {
            ProjectTemplateType.StudyProject => "Notes/requirements.md",
            ProjectTemplateType.FreelanceProject => "Client/requirements.md",
            ProjectTemplateType.CreativeProject => "Notes/project-idea.md",
            ProjectTemplateType.ResearchProject => "Notes/research-question.md",
            _ => "Planning/project-plan.md"
        };

        return files.Any(x => string.Equals(x.RelativePath, preferred, StringComparison.OrdinalIgnoreCase))
            ? preferred
            : files.FirstOrDefault()?.RelativePath ?? string.Empty;
    }

    private static IReadOnlyList<TaskItem> BuildLegacyTasks(TemplatePlanNode root, string projectDirectory)
    {
        var result = new List<TaskItem>();
        foreach (var node in Flatten(root).Where(x => x.Type is WorkItemType.Task or WorkItemType.Subtask).Take(8))
        {
            result.Add(new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = node.Title,
                Description = node.Description,
                Status = node.Status == WorkItemStatus.InProgress ? "In Progress" : "To Do",
                Priority = node.Priority,
                DueDateUtc = node.Deadline,
                AttachedFilePath = NormalizeProjectPath(projectDirectory, node.LinkedPath)
            });
        }

        return result;
    }

    private static IEnumerable<TemplatePlanNode> Flatten(TemplatePlanNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var nested in Flatten(child))
            {
                yield return nested;
            }
        }
    }

    private static async Task WriteStarterFileAsync(string projectDirectory, string relativePath, string content, CancellationToken cancellationToken)
    {
        var path = Path.Combine(projectDirectory, relativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(path))
        {
            await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<string> ParseKnownSections(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static string DefaultFinalGoal(ProjectTemplateType templateType, string mainTopic, string desiredResult)
    {
        var subject = FirstNonEmpty(mainTopic, desiredResult, "the project");
        return templateType switch
        {
            ProjectTemplateType.StudyProject => $"Study {subject} and prepare a result that can be submitted or reused.",
            ProjectTemplateType.FreelanceProject => $"Deliver {subject} to the client according to the agreed requirements.",
            ProjectTemplateType.CreativeProject => $"Create a finished creative result for {subject}.",
            ProjectTemplateType.ResearchProject => $"Research {subject} and produce a clear conclusion or report.",
            _ => "Define the final goal and turn the idea into a workable plan."
        };
    }

    private static string DefaultNextStep(ProjectTemplateType templateType)
    {
        return templateType switch
        {
            ProjectTemplateType.StudyProject => "write the topic and requirements",
            ProjectTemplateType.FreelanceProject => "clarify client requirements",
            ProjectTemplateType.CreativeProject => "write the idea in one sentence",
            ProjectTemplateType.ResearchProject => "formulate the main research question",
            _ => "define the final goal"
        };
    }

    private static bool IsMinimal(CreateProjectRequest request)
    {
        return string.Equals(request.DetailLevel, "Minimal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDetailed(CreateProjectRequest request)
    {
        return string.Equals(request.DetailLevel, "Detailed", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static string NormalizeProjectPath(string? projectDirectory, string linkedPath)
    {
        if (string.IsNullOrWhiteSpace(linkedPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(linkedPath) || string.IsNullOrWhiteSpace(projectDirectory))
        {
            return linkedPath;
        }

        return Path.Combine(projectDirectory, linkedPath);
    }

    public async Task<ProjectMeta?> OpenProjectFromDialogAsync(CancellationToken cancellationToken = default)
    {
        var selectedFolder = _dialogService.ShowOpenFolderDialog();
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            return null;
        }

        return await OpenProjectAsync(selectedFolder, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectMeta?> OpenProjectAsync(string projectFolderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectFolderPath) || !Directory.Exists(projectFolderPath))
        {
            return null;
        }

        var ritPath = Path.Combine(projectFolderPath, ".reachit.json");
        var meta = await LoadProjectMetaFromRitAsync(ritPath, projectFolderPath, cancellationToken).ConfigureAwait(false);
        if (meta is null)
        {
            return null;
        }

        if (!File.Exists(meta.RitFilePath))
        {
            await SaveRitFileAsync(meta, cancellationToken).ConfigureAwait(false);
        }

        await UpsertProjectMetaAsync(meta, cancellationToken).ConfigureAwait(false);

        CurrentProject = meta;
        await _recentFilesService.AddRecentProjectAsync(meta, cancellationToken).ConfigureAwait(false);
        return meta;
    }

    public async Task SaveProjectAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return;
        }

        CurrentProject.UpdatedAtUtc = DateTime.UtcNow;
        await UpsertProjectMetaAsync(CurrentProject, cancellationToken).ConfigureAwait(false);
        await SaveRitFileAsync(CurrentProject, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveAllAsync(CancellationToken cancellationToken = default)
    {
        // TODO: Save task manager, focus, and settings state snapshot together with .rit metadata.
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ProjectTreeNode>> GetCurrentTreeAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return [];
        }

        return await _fileSystemProjectExplorerService.BuildTreeAsync(CurrentProject, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectTreeNode?> CreateInternalFileAsync(ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return null;
        }

        return await _fileSystemProjectExplorerService.CreateFileAsync(CurrentProject, selectedNode, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProjectTreeNode?> CreateFolderAsync(ProjectTreeNode? selectedNode, CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return null;
        }

        return await _fileSystemProjectExplorerService.CreateFolderAsync(CurrentProject, selectedNode, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<ProjectMeta>> GetRecentProjectsAsync(CancellationToken cancellationToken = default)
    {
        return _recentFilesService.GetRecentProjectsAsync(cancellationToken);
    }

    private async Task<ProjectMeta?> LoadProjectMetaFromRitAsync(string ritFilePath, string projectFolderPath, CancellationToken cancellationToken)
    {
        if (File.Exists(ritFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(ritFilePath, cancellationToken).ConfigureAwait(false);
                var meta = JsonSerializer.Deserialize<ProjectMeta>(json);
                if (meta is not null)
                {
                    if (meta.Id == Guid.Empty)
                    {
                        meta.Id = Guid.NewGuid();
                    }

                    meta.ProjectDirectoryPath = projectFolderPath;
                    meta.RitFilePath = ritFilePath;
                    meta.UpdatedAtUtc = DateTime.UtcNow;
                    return meta;
                }
            }
            catch (JsonException)
            {
                // TODO: Support legacy JSON formats.
            }
        }

        var fallback = new ProjectMeta
        {
            Id = Guid.NewGuid(),
            ProjectName = Path.GetFileName(projectFolderPath),
            ProjectDirectoryPath = projectFolderPath,
            RitFilePath = ritFilePath,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await SaveRitFileAsync(fallback, cancellationToken).ConfigureAwait(false);
        return fallback;
    }

    private async Task SaveRitFileAsync(ProjectMeta meta, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(meta.RitFilePath, payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertProjectMetaAsync(ProjectMeta meta, CancellationToken cancellationToken)
    {
        await using var dbContext = _databaseService.CreateDbContext();
        var existing = await dbContext.Projects.FirstOrDefaultAsync(x => x.Id == meta.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            dbContext.Projects.Add(meta);
        }
        else
        {
            existing.ProjectName = meta.ProjectName;
            existing.Description = meta.Description;
            existing.ProjectDirectoryPath = meta.ProjectDirectoryPath;
            existing.RitFilePath = meta.RitFilePath;
            existing.TemplateType = meta.TemplateType;
            existing.UseProjectActivitySettings = meta.UseProjectActivitySettings;
            existing.ProjectEnableActivityTracking = meta.ProjectEnableActivityTracking;
            existing.ProjectTrackActiveWindow = meta.ProjectTrackActiveWindow;
            existing.ProjectTrackFileChanges = meta.ProjectTrackFileChanges;
            existing.ProjectTrackGitChanges = meta.ProjectTrackGitChanges;
            existing.ProjectTrackTextStatistics = meta.ProjectTrackTextStatistics;
            existing.ProjectPauseActivityTracking = meta.ProjectPauseActivityTracking;
            existing.ProjectIgnoredFoldersSerialized = meta.ProjectIgnoredFoldersSerialized;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class TemplateProjectPlan
    {
        public IReadOnlyList<StarterFile> StarterFiles { get; init; } = [];
        public IReadOnlyList<TemplatePlanNode> Items { get; init; } = [];
        public IReadOnlyList<TaskItem> LegacyTasks { get; init; } = [];
    }

    private sealed class TemplatePlanNode
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public WorkItemType Type { get; init; } = WorkItemType.Task;
        public WorkItemStatus Status { get; init; } = WorkItemStatus.Backlog;
        public int Priority { get; init; } = 2;
        public DateTime? Deadline { get; init; }
        public double EstimatedWorkUnits { get; init; } = 1;
        public string LinkedPath { get; init; } = string.Empty;
        public string Tags { get; init; } = string.Empty;
        public string Notes { get; init; } = string.Empty;
        public List<TemplatePlanNode> Children { get; } = [];
    }

    private sealed record StarterFile(string RelativePath, string Content);
}
