// Defines logical node types shown in the project explorer.
namespace ReachIT.Domain.Enums;

public enum ProjectTreeNodeType
{
    ProjectRoot = 0,
    Folder = 1,
    File = 2,
    RitConfigFile = 3,
    ExternalFileLink = 4,
    WebLink = 5,
    OfflinePage = 6,
    VirtualNode = 7
}
