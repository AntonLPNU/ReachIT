using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ReachIT.Domain.Models;

namespace ReachIT.Presentation.Windows;

public partial class AttachTaskWindow : Window
{
    private readonly IReadOnlyList<TaskItem> _allTasks;
    private readonly ObservableCollection<TaskItem> _filteredTasks = new();

    public AttachTaskWindow(string filePath, IReadOnlyList<TaskItem> existingTasks)
    {
        InitializeComponent();

        FilePathText.Text = filePath;
        TitleBox.Text = BuildDefaultTitle(filePath);
        DueDatePicker.SelectedDate = DateTime.Today.AddDays(1);

        _allTasks = existingTasks
            .Where(t => !t.IsCompleted)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.DueDateUtc)
            .ThenBy(t => t.Title)
            .ToList();

        ExistingTasksList.ItemsSource = _filteredTasks;
        RefreshFilteredTasks();

        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        };
    }

    public AttachTaskResult? Result { get; private set; }

    private void OnCreateClick(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ValidationText.Text = "Add a short task title first.";
            TitleBox.Focus();
            return;
        }

        Result = AttachTaskResult.CreateNew(
            title,
            DescriptionBox.Text.Trim(),
            DueDatePicker.SelectedDate);
        DialogResult = true;
    }

    private void OnAttachExistingClick(object sender, RoutedEventArgs e)
    {
        AttachSelectedExistingTask();
    }

    private void OnExistingTaskDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AttachSelectedExistingTask();
    }

    private void AttachSelectedExistingTask()
    {
        if (ExistingTasksList.SelectedItem is not TaskItem selectedTask)
        {
            return;
        }

        Result = AttachTaskResult.AttachExisting(selectedTask.Id);
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnTaskSearchChanged(object sender, TextChangedEventArgs e)
    {
        RefreshFilteredTasks();
    }

    private void OnExistingTaskSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AttachExistingButton.IsEnabled = ExistingTasksList.SelectedItem is TaskItem;
    }

    private void RefreshFilteredTasks()
    {
        var query = TaskSearchBox.Text.Trim();
        var tasks = string.IsNullOrWhiteSpace(query)
            ? _allTasks
            : _allTasks
                .Where(t => Contains(t.Title, query) ||
                            Contains(t.Description, query) ||
                            Contains(t.AttachedFilePath ?? string.Empty, query))
                .ToList();

        _filteredTasks.Clear();
        foreach (var task in tasks)
        {
            _filteredTasks.Add(task);
        }

        AttachExistingButton.IsEnabled = ExistingTasksList.SelectedItem is TaskItem;
    }

    private static bool Contains(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDefaultTitle(string filePath)
    {
        var name = Path.GetFileName(filePath);
        return string.IsNullOrWhiteSpace(name)
            ? "Update selected file"
            : $"Work on {name}";
    }
}

public sealed class AttachTaskResult
{
    private AttachTaskResult(bool createNew, Guid? existingTaskId, string title, string description, DateTime? dueDate)
    {
        CreateNewTask = createNew;
        ExistingTaskId = existingTaskId;
        Title = title;
        Description = description;
        DueDate = dueDate;
    }

    public bool CreateNewTask { get; }
    public Guid? ExistingTaskId { get; }
    public string Title { get; }
    public string Description { get; }
    public DateTime? DueDate { get; }

    public static AttachTaskResult CreateNew(string title, string description, DateTime? dueDate)
    {
        return new AttachTaskResult(true, null, title, description, dueDate);
    }

    public static AttachTaskResult AttachExisting(Guid taskId)
    {
        return new AttachTaskResult(false, taskId, string.Empty, string.Empty, null);
    }
}
