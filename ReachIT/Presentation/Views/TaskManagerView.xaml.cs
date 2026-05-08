// Code-behind placeholder for TaskManagerView.
using System.Windows.Controls;
using ReachIT.Domain.Models;
using ReachIT.Presentation.ViewModels;

namespace ReachIT.Presentation.Views;

public partial class TaskManagerView : UserControl
{
    public TaskManagerView()
    {
        InitializeComponent();
    }

    private void TaskTree_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is TaskManagerViewModel viewModel && e.NewValue is TaskItem task)
        {
            viewModel.SelectedTask = task;
        }
    }
}
