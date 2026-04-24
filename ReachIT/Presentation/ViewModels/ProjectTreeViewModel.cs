// Provides explorer tree state and helper actions.
using System.Collections.ObjectModel;
using ReachIT.Domain.Models;

namespace ReachIT.Presentation.ViewModels;

public sealed class ProjectTreeViewModel : ViewModelBase
{
    private ProjectTreeNode? _selectedNode;

    public ObservableCollection<ProjectTreeNode> Nodes { get; } = new();

    public ProjectTreeNode? SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value);
    }

    public void SetNodes(IEnumerable<ProjectTreeNode> nodes)
    {
        Nodes.Clear();
        foreach (var node in nodes)
        {
            Nodes.Add(node);
        }
    }

    public void CollapseAll()
    {
        foreach (var node in Nodes)
        {
            CollapseRecursive(node);
        }
    }

    private static void CollapseRecursive(ProjectTreeNode node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
        {
            CollapseRecursive(child);
        }
    }
}
