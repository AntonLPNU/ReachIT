// Carries create-project form values from UI to project service.
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class CreateProjectRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SaveLocation { get; set; } = string.Empty;
    public ProjectTemplateType TemplateType { get; set; } = ProjectTemplateType.EmptyProject;
    public string FinalGoal { get; set; } = string.Empty;
    public string MainTopic { get; set; } = string.Empty;
    public string DesiredResult { get; set; } = string.Empty;
    public string ResultFormat { get; set; } = string.Empty;
    public string KnownSections { get; set; } = string.Empty;
    public string DetailLevel { get; set; } = "Medium";
    public DateTime? DeadlineDate { get; set; }
    public bool CreateStarterFiles { get; set; } = true;
    public bool LinkTasksToFiles { get; set; } = true;
    public List<string> InitialExternalFiles { get; set; } = [];
}
