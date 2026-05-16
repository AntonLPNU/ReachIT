using System.Windows;

namespace ReachIT.Presentation.Views;

public partial class TaskDiagramStyleWindow : Window
{
    public TaskDiagramStyleWindow()
    {
        InitializeComponent();
    }

    public TaskDiagramExportFormat SelectedFormat { get; private set; } = TaskDiagramExportFormat.Png;
    public bool ExportReachItStyle { get; private set; } = true;

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        SelectedFormat = DrawIoOption.IsChecked == true
            ? TaskDiagramExportFormat.DrawIo
            : SvgOption.IsChecked == true
                ? TaskDiagramExportFormat.Svg
                : TaskDiagramExportFormat.Png;
        ExportReachItStyle = ReachItStyleOption.IsChecked == true;

        DialogResult = true;
        Close();
    }
}

public enum TaskDiagramExportFormat
{
    Png,
    Svg,
    DrawIo
}
