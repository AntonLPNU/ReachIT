// Provides explorer tree state and helper actions.
using System.Collections.ObjectModel;
using System.IO;
using ReachIT.Domain.Enums;
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

    public void SetNodes(
        IEnumerable<ProjectTreeNode> nodes,
        bool preserveState = true,
        string? preferredSelectedPath = null,
        IEnumerable<string>? additionalExpandedPaths = null)
    {
        var expandedNodeKeys = preserveState
            ? CaptureExpandedNodeKeys()
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (additionalExpandedPaths is not null)
        {
            foreach (var additionalExpandedPath in additionalExpandedPaths)
            {
                var normalizedPath = NormalizePath(additionalExpandedPath);
                if (!string.IsNullOrWhiteSpace(normalizedPath))
                {
                    expandedNodeKeys.Add(normalizedPath);
                }
            }
        }

        var selectedNodeKey = preferredSelectedPath switch
        {
            not null => NormalizePath(preferredSelectedPath),
            _ when preserveState => GetNodeKey(SelectedNode),
            _ => null
        };

        Nodes.Clear();
        foreach (var node in nodes)
        {
            Nodes.Add(node);
        }

        if (!preserveState)
        {
            SelectedNode = null;
            return;
        }

        RestoreExpandedState(Nodes, expandedNodeKeys);
        RestoreSelectionState(selectedNodeKey);
    }

    public bool TryAddNode(ProjectTreeNode node, string parentDirectoryPath)
    {
        var normalizedParentPath = NormalizePath(parentDirectoryPath);
        if (string.IsNullOrWhiteSpace(normalizedParentPath))
        {
            return false;
        }

        var parentNode = FindNodeByKey(Nodes, normalizedParentPath);
        if (parentNode is null || !parentNode.IsDirectory)
        {
            return false;
        }

        if (parentNode.Children.Any(child => string.Equals(GetNodeKey(child), GetNodeKey(node), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        node.ParentId = parentNode.Id;
        InsertSorted(parentNode.Children, node);
        EnsureExpandedByPath(parentNode.FullPath);
        SelectNode(node);
        return true;
    }

    public bool TryRemoveNode(string fullPath, out ProjectTreeNode? fallbackSelectionNode)
    {
        fallbackSelectionNode = null;
        var normalizedPath = NormalizePath(fullPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        if (!TryRemoveNodeRecursive(Nodes, normalizedPath, parentNode: null, out var removedNode, out var removedParentNode))
        {
            return false;
        }

        if (removedNode is not null && string.Equals(GetNodeKey(SelectedNode), GetNodeKey(removedNode), StringComparison.OrdinalIgnoreCase))
        {
            SelectNode(removedParentNode);
            fallbackSelectionNode = removedParentNode;
        }

        return true;
    }

    public bool TryRenameNode(ProjectTreeNode node, string newName, out string? renamedFullPath)
    {
        renamedFullPath = null;

        var oldFullPath = node.FullPath;
        var parentDirectory = Path.GetDirectoryName(oldFullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return false;
        }

        var updatedName = newName.Trim();
        var newFullPath = Path.Combine(parentDirectory, updatedName);
        var rootPath = Nodes.FirstOrDefault(x => x.NodeType == ProjectTreeNodeType.ProjectRoot)?.FullPath;

        node.Name = updatedName;
        UpdateNodePathRecursive(node, oldFullPath, newFullPath, rootPath);

        var parentNode = FindNodeByKey(Nodes, NormalizePath(parentDirectory) ?? parentDirectory);
        if (parentNode is not null)
        {
            var existingIndex = parentNode.Children.IndexOf(node);
            if (existingIndex >= 0)
            {
                parentNode.Children.RemoveAt(existingIndex);
                InsertSorted(parentNode.Children, node);
            }
        }

        EnsureExpandedByPath(parentDirectory);
        SelectNode(node);
        renamedFullPath = node.FullPath;
        return true;
    }

    public void EnsureExpandedByPath(string? fullPath)
    {
        var normalizedPath = NormalizePath(fullPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        if (TryFindNodeAndAncestors(Nodes, normalizedPath, new List<ProjectTreeNode>(), out var ancestors, out var foundNode)
            && foundNode is not null)
        {
            foreach (var ancestor in ancestors)
            {
                ancestor.IsExpanded = true;
            }

            if (foundNode.IsDirectory)
            {
                foundNode.IsExpanded = true;
            }
        }
    }

    public void SelectNodeByPath(string? fullPath)
    {
        var normalizedPath = NormalizePath(fullPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            SelectNode(null);
            return;
        }

        var node = FindNodeByKey(Nodes, normalizedPath);
        SelectNode(node);
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

    private HashSet<string> CaptureExpandedNodeKeys()
    {
        var expandedNodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rootNode in Nodes)
        {
            CaptureExpandedNodeKeysRecursive(rootNode, expandedNodeKeys);
        }

        return expandedNodeKeys;
    }

    private static void CaptureExpandedNodeKeysRecursive(ProjectTreeNode node, HashSet<string> expandedNodeKeys)
    {
        if (node.IsExpanded)
        {
            var key = GetNodeKey(node);
            if (!string.IsNullOrWhiteSpace(key))
            {
                expandedNodeKeys.Add(key);
            }
        }

        foreach (var child in node.Children)
        {
            CaptureExpandedNodeKeysRecursive(child, expandedNodeKeys);
        }
    }

    private static void RestoreExpandedState(IEnumerable<ProjectTreeNode> nodes, HashSet<string> expandedNodeKeys)
    {
        foreach (var node in nodes)
        {
            var key = GetNodeKey(node);
            node.IsExpanded = !string.IsNullOrWhiteSpace(key) && expandedNodeKeys.Contains(key);
            RestoreExpandedState(node.Children, expandedNodeKeys);
        }
    }

    private void RestoreSelectionState(string? selectedNodeKey)
    {
        if (string.IsNullOrWhiteSpace(selectedNodeKey))
        {
            SelectNode(null);
            return;
        }

        var restoredNode = FindNodeByKey(Nodes, selectedNodeKey);
        if (restoredNode is null)
        {
            SelectNode(null);
            return;
        }

        SelectNode(restoredNode);
    }

    private static ProjectTreeNode? FindNodeByKey(IEnumerable<ProjectTreeNode> nodes, string key)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(GetNodeKey(node), key, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            var childMatch = FindNodeByKey(node.Children, key);
            if (childMatch is not null)
            {
                return childMatch;
            }
        }

        return null;
    }

    private static string? GetNodeKey(ProjectTreeNode? node)
    {
        if (node is null || string.IsNullOrWhiteSpace(node.FullPath))
        {
            return null;
        }

        if (node.NodeType is ReachIT.Domain.Enums.ProjectTreeNodeType.VirtualNode
            or ReachIT.Domain.Enums.ProjectTreeNodeType.WebLink
            or ReachIT.Domain.Enums.ProjectTreeNodeType.OfflinePage
            or ReachIT.Domain.Enums.ProjectTreeNodeType.ExternalFileLink)
        {
            return $"{node.NodeType}:{node.FullPath}";
        }

        try
        {
            return NormalizePath(node.FullPath);
        }
        catch (Exception)
        {
            return node.FullPath;
        }
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    private void SelectNode(ProjectTreeNode? node)
    {
        ClearSelection(Nodes);
        if (node is not null)
        {
            node.IsSelected = true;
        }

        SelectedNode = node;
    }

    private static void ClearSelection(IEnumerable<ProjectTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsSelected = false;
            ClearSelection(node.Children);
        }
    }

    private static bool TryRemoveNodeRecursive(
        ObservableCollection<ProjectTreeNode> nodes,
        string normalizedTargetPath,
        ProjectTreeNode? parentNode,
        out ProjectTreeNode? removedNode,
        out ProjectTreeNode? removedParentNode)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (string.Equals(GetNodeKey(node), normalizedTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                removedNode = node;
                removedParentNode = parentNode;
                nodes.RemoveAt(i);
                return true;
            }

            if (TryRemoveNodeRecursive(node.Children, normalizedTargetPath, node, out removedNode, out removedParentNode))
            {
                return true;
            }
        }

        removedNode = null;
        removedParentNode = null;
        return false;
    }

    private static void InsertSorted(ObservableCollection<ProjectTreeNode> collection, ProjectTreeNode node)
    {
        var index = 0;
        while (index < collection.Count && CompareNodes(collection[index], node) <= 0)
        {
            index++;
        }

        collection.Insert(index, node);
    }

    private static int CompareNodes(ProjectTreeNode left, ProjectTreeNode right)
    {
        if (left.IsDirectory != right.IsDirectory)
        {
            return left.IsDirectory ? -1 : 1;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static void UpdateNodePathRecursive(ProjectTreeNode node, string oldPrefix, string newPrefix, string? projectRootPath)
    {
        if (string.Equals(node.FullPath, oldPrefix, StringComparison.OrdinalIgnoreCase))
        {
            node.FullPath = newPrefix;
        }
        else if (node.FullPath.StartsWith(oldPrefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            node.FullPath = newPrefix + node.FullPath[oldPrefix.Length..];
        }

        if (!string.IsNullOrWhiteSpace(projectRootPath))
        {
            node.RelativePath = string.Equals(node.FullPath, projectRootPath, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : Path.GetRelativePath(projectRootPath, node.FullPath);
        }

        foreach (var child in node.Children)
        {
            UpdateNodePathRecursive(child, oldPrefix, newPrefix, projectRootPath);
        }
    }

    private static bool TryFindNodeAndAncestors(
        IEnumerable<ProjectTreeNode> nodes,
        string key,
        List<ProjectTreeNode> currentAncestors,
        out List<ProjectTreeNode> ancestors,
        out ProjectTreeNode? foundNode)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(GetNodeKey(node), key, StringComparison.OrdinalIgnoreCase))
            {
                ancestors = currentAncestors;
                foundNode = node;
                return true;
            }

            var childAncestors = new List<ProjectTreeNode>(currentAncestors) { node };
            if (TryFindNodeAndAncestors(node.Children, key, childAncestors, out ancestors, out foundNode))
            {
                return true;
            }
        }

        ancestors = currentAncestors;
        foundNode = null;
        return false;
    }
}
