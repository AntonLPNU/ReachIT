// Carries create-project form values from UI to project service.
using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class CreateProjectRequest
{
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SaveLocation { get; set; } = string.Empty;
    public ProjectTemplateType TemplateType { get; set; } = ProjectTemplateType.EmptyProject;
    public List<string> InitialExternalFiles { get; set; } = [];
}
