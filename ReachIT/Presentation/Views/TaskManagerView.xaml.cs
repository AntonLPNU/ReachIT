// Code-behind for task tree selection and diagram export.
using System.IO;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ReachIT.Domain.Models;
using ReachIT.Presentation.ViewModels;

namespace ReachIT.Presentation.Views;

public partial class TaskManagerView : UserControl
{
    private const double MinDiagramZoom = 0.25;
    private const double MaxDiagramZoom = 3.5;
    private Point _lastPanPoint;
    private bool _isPanningDiagram;

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

    private void ExportDiagram_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TaskManagerViewModel viewModel)
        {
            return;
        }

        var exportWindow = new TaskDiagramStyleWindow
        {
            Owner = System.Windows.Window.GetWindow(this)
        };

        if (exportWindow.ShowDialog() != true)
        {
            return;
        }

        viewModel.IsCanvasView = true;
        var previousStyle = viewModel.DiagramStyle;
        viewModel.DiagramStyle = exportWindow.ExportReachItStyle
            ? TaskDiagramStyle.ReachIt
            : TaskDiagramStyle.Classic;

        try
        {
            switch (exportWindow.SelectedFormat)
            {
                case TaskDiagramExportFormat.Svg:
                    ExportSvg(viewModel);
                    break;
                case TaskDiagramExportFormat.DrawIo:
                    ExportDrawIo(viewModel);
                    break;
                default:
                    ExportPng();
                    break;
            }
        }
        finally
        {
            viewModel.DiagramStyle = previousStyle;
        }
    }

    private void ExportPng()
    {
        var previousZoom = TaskDiagramScaleTransform.ScaleX;
        var previousPanX = TaskDiagramTranslateTransform.X;
        var previousPanY = TaskDiagramTranslateTransform.Y;
        ResetDiagramViewport();
        TaskDiagramCanvas.UpdateLayout();

        var dialog = new SaveFileDialog
        {
            Title = "Export task diagram as PNG",
            Filter = "PNG image (*.png)|*.png",
            FileName = "reachit-task-diagram.png",
            AddExtension = true,
            DefaultExt = ".png"
        };

        try
        {
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var width = Math.Max(1, (int)Math.Ceiling(TaskDiagramCanvas.ActualWidth));
            var height = Math.Max(1, (int)Math.Ceiling(TaskDiagramCanvas.ActualHeight));
            var exportSurface = new DrawingVisual();
            using (var context = exportSurface.RenderOpen())
            {
                var background = DataContext is TaskManagerViewModel { IsClassicDiagramStyle: true }
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromRgb(0x10, 0x18, 0x27));
                context.DrawRectangle(background, null, new Rect(0, 0, width, height));
                context.DrawRectangle(new VisualBrush(TaskDiagramCanvas), null, new Rect(0, 0, width, height));
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(exportSurface);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);
        }
        finally
        {
            SetDiagramViewport(previousZoom, previousPanX, previousPanY);
        }
    }

    private void ExportSvg(TaskManagerViewModel viewModel)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export task diagram as SVG",
            Filter = "SVG vector image (*.svg)|*.svg",
            FileName = "reachit-task-diagram.svg",
            AddExtension = true,
            DefaultExt = ".svg"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildSvg(viewModel), Encoding.UTF8);
    }

    private void ExportDrawIo(TaskManagerViewModel viewModel)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export task diagram as draw.io",
            Filter = "draw.io diagram (*.drawio)|*.drawio",
            FileName = "reachit-task-diagram.drawio",
            AddExtension = true,
            DefaultExt = ".drawio"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildDrawIo(viewModel), Encoding.UTF8);
    }

    private static string BuildSvg(TaskManagerViewModel viewModel)
    {
        var classic = viewModel.IsClassicDiagramStyle;
        var background = classic ? "#FFFFFF" : "#101827";
        var nodeFill = classic ? "#FFFFFF" : "#121C2F";
        var rootFill = classic ? "#F2F2F2" : "#1B304F";
        var border = classic ? "#111111" : "#233A5D";
        var rootBorder = classic ? "#111111" : "#4CC9F0";
        var text = classic ? "#111111" : "#EAF0FF";
        var muted = classic ? "#444444" : "#98A4BA";
        var accent = classic ? "#111111" : "#4CC9F0";
        var width = Math.Max(1, (int)Math.Ceiling(viewModel.CanvasWidth));
        var height = Math.Max(1, (int)Math.Ceiling(viewModel.CanvasHeight));

        var svg = new StringBuilder();
        svg.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">""");
        svg.AppendLine($"""  <rect width="100%" height="100%" fill="{background}" />""");

        foreach (var connector in viewModel.CanvasConnectors)
        {
            svg.AppendLine($"""  <path d="M {connector.X1:0.##} {connector.Y1:0.##} V {connector.MidY:0.##} H {connector.X2:0.##} V {connector.Y2:0.##}" fill="none" stroke="{accent}" stroke-width="2" stroke-linecap="round" />""");
        }

        foreach (var node in viewModel.CanvasTasks)
        {
            var fill = node.IsProjectRoot ? rootFill : nodeFill;
            var stroke = node.IsProjectRoot ? rootBorder : border;
            svg.AppendLine($"""  <rect x="{node.Left:0.##}" y="{node.Top:0.##}" width="{node.Width:0.##}" height="{node.Height:0.##}" rx="8" fill="{fill}" stroke="{stroke}" stroke-width="1.5" opacity="{(node.IsCompleted ? "0.68" : "1")}" />""");
            svg.AppendLine($"""  <text x="{node.Left + 14:0.##}" y="{node.Top + 27:0.##}" fill="{text}" font-family="Segoe UI, Arial, sans-serif" font-size="14" font-weight="700">{Escape(node.Title)}</text>""");

            var description = TrimForSvg(node.Description, 40);
            if (!string.IsNullOrWhiteSpace(description))
            {
                svg.AppendLine($"""  <text x="{node.Left + 14:0.##}" y="{node.Top + 52:0.##}" fill="{muted}" font-family="Segoe UI, Arial, sans-serif" font-size="12">{Escape(description)}</text>""");
            }

            svg.AppendLine($"""  <text x="{node.Left + 14:0.##}" y="{node.Top + node.Height - 17:0.##}" fill="{accent}" font-family="Segoe UI, Arial, sans-serif" font-size="12" font-weight="700">{Escape(node.Status)}</text>""");
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static string BuildDrawIo(TaskManagerViewModel viewModel)
    {
        var width = Math.Max(850, (int)Math.Ceiling(viewModel.CanvasWidth + 80));
        var height = Math.Max(600, (int)Math.Ceiling(viewModel.CanvasHeight + 80));
        var nodeIds = viewModel.CanvasTasks
            .Select((node, index) => new { node, id = $"node_{index}" })
            .ToDictionary(x => x.node, x => x.id);

        var xml = new StringBuilder();
        xml.AppendLine($"""<mxfile host="app.diagrams.net" modified="{DateTime.UtcNow:O}" agent="ReachIT" version="24.7.17">""");
        xml.AppendLine("""  <diagram id="reachit-task-diagram" name="Task diagram">""");
        xml.AppendLine($"""    <mxGraphModel dx="{width}" dy="{height}" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{width}" pageHeight="{height}" background="#ffffff" math="0" shadow="0">""");
        xml.AppendLine("""      <root>""");
        xml.AppendLine("""        <mxCell id="0" />""");
        xml.AppendLine("""        <mxCell id="1" parent="0" />""");

        foreach (var node in viewModel.CanvasTasks)
        {
            var id = nodeIds[node];
            var label = $"{Escape(node.Title)}&#xa;{Escape(node.Status)}";
            var fill = node.IsProjectRoot ? "#f2f2f2" : "#ffffff";
            var style = $"rounded=1;whiteSpace=wrap;html=1;fillColor={fill};strokeColor=#111111;fontColor=#111111;";
            xml.AppendLine($"""        <mxCell id="{id}" value="{label}" style="{style}" vertex="1" parent="1">""");
            xml.AppendLine($"""          <mxGeometry x="{node.Left:0.##}" y="{node.Top:0.##}" width="{node.Width:0.##}" height="{node.Height:0.##}" as="geometry" />""");
            xml.AppendLine("""        </mxCell>""");
        }

        var edgeIndex = 0;
        foreach (var connector in viewModel.CanvasConnectors)
        {
            var source = FindSourceNode(viewModel, connector);
            var target = FindTargetNode(viewModel, connector);
            if (source is null || target is null)
            {
                continue;
            }

            var edgeId = $"edge_{edgeIndex++}";
            xml.AppendLine($"""        <mxCell id="{edgeId}" value="" style="edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;strokeColor=#111111;endArrow=none;" edge="1" parent="1" source="{nodeIds[source]}" target="{nodeIds[target]}">""");
            xml.AppendLine("""          <mxGeometry relative="1" as="geometry" />""");
            xml.AppendLine("""        </mxCell>""");
        }

        xml.AppendLine("""      </root>""");
        xml.AppendLine("""    </mxGraphModel>""");
        xml.AppendLine("""  </diagram>""");
        xml.AppendLine("""</mxfile>""");
        return xml.ToString();
    }

    private static TaskCanvasNode? FindSourceNode(TaskManagerViewModel viewModel, TaskCanvasConnector connector)
    {
        return viewModel.CanvasTasks.FirstOrDefault(node =>
            Math.Abs(node.CenterX - connector.X1) < 0.1 &&
            Math.Abs(node.Bottom - connector.Y1) < 0.1);
    }

    private static TaskCanvasNode? FindTargetNode(TaskManagerViewModel viewModel, TaskCanvasConnector connector)
    {
        return viewModel.CanvasTasks.FirstOrDefault(node =>
            Math.Abs(node.CenterX - connector.X2) < 0.1 &&
            Math.Abs(node.Top - connector.Y2) < 0.1);
    }

    private static string TrimForSvg(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var compact = value.ReplaceLineEndings(" ").Trim();
        return compact.Length <= maxLength ? compact : compact[..Math.Max(0, maxLength - 1)] + "...";
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }

    private void TaskDiagramViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mouse = e.GetPosition(TaskDiagramViewport);
        var oldScale = TaskDiagramScaleTransform.ScaleX;
        var zoomFactor = e.Delta > 0 ? 1.12 : 1 / 1.12;
        var newScale = Math.Clamp(oldScale * zoomFactor, MinDiagramZoom, MaxDiagramZoom);

        if (Math.Abs(newScale - oldScale) < 0.001)
        {
            e.Handled = true;
            return;
        }

        var canvasX = (mouse.X - TaskDiagramTranslateTransform.X) / oldScale;
        var canvasY = (mouse.Y - TaskDiagramTranslateTransform.Y) / oldScale;

        TaskDiagramScaleTransform.ScaleX = newScale;
        TaskDiagramScaleTransform.ScaleY = newScale;
        TaskDiagramTranslateTransform.X = mouse.X - canvasX * newScale;
        TaskDiagramTranslateTransform.Y = mouse.Y - canvasY * newScale;
        e.Handled = true;
    }

    private void TaskDiagramViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanningDiagram = true;
        _lastPanPoint = e.GetPosition(TaskDiagramViewport);
        TaskDiagramViewport.CaptureMouse();
        TaskDiagramViewport.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void TaskDiagramViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDiagramPan();
        e.Handled = true;
    }

    private void TaskDiagramViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanningDiagram || e.LeftButton != MouseButtonState.Pressed)
        {
            if (_isPanningDiagram)
            {
                EndDiagramPan();
            }

            return;
        }

        var current = e.GetPosition(TaskDiagramViewport);
        var delta = current - _lastPanPoint;
        TaskDiagramTranslateTransform.X += delta.X;
        TaskDiagramTranslateTransform.Y += delta.Y;
        _lastPanPoint = current;
        e.Handled = true;
    }

    private void EndDiagramPan()
    {
        if (!_isPanningDiagram)
        {
            return;
        }

        _isPanningDiagram = false;
        TaskDiagramViewport.ReleaseMouseCapture();
        TaskDiagramViewport.Cursor = Cursors.Hand;
    }

    private void ResetDiagramViewport()
    {
        SetDiagramViewport(1, 0, 0);
    }

    private void SetDiagramViewport(double scale, double x, double y)
    {
        TaskDiagramScaleTransform.ScaleX = scale;
        TaskDiagramScaleTransform.ScaleY = scale;
        TaskDiagramTranslateTransform.X = x;
        TaskDiagramTranslateTransform.Y = y;
    }
}
