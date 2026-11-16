using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace WasteVision.Desktop.Common;

public sealed class CurveChartControl : UserControl
{
    private readonly Canvas _canvas;
    private readonly TextBlock _titleText;
    private readonly TextBlock _legendTrain;
    private readonly TextBlock _legendVal;
    private readonly TextBlock _yMaxLabel;
    private readonly TextBlock _yMinLabel;

    public CurveChartControl(string title, string trainLabel = "تدريب (Train)", string valLabel = "تحقق (Val)")
    {
        var mainStack = new StackPanel { Spacing = 8 };

        // Header
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        _titleText = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = UiFactory.ThemeBrush("TextPrimaryBrush")
        };
        Grid.SetColumn(_titleText, 0);

        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        _legendTrain = new TextBlock
        {
            Text = $"● {trainLabel}",
            FontSize = 11,
            Foreground = UiFactory.ThemeBrush("WasteMintBrush")
        };
        _legendVal = new TextBlock
        {
            Text = $"● {valLabel}",
            FontSize = 11,
            Foreground = UiFactory.ThemeBrush("WasteCyanBrush")
        };
        legend.Children.Add(_legendTrain);
        legend.Children.Add(_legendVal);
        Grid.SetColumn(legend, 1);

        headerGrid.Children.Add(_titleText);
        headerGrid.Children.Add(legend);
        mainStack.Children.Add(headerGrid);

        // Chart Viewport container
        var chartBox = new Grid
        {
            Height = 170,
            Background = UiFactory.ThemeBrush("TileSoftBackgroundBrush"),
            BorderBrush = UiFactory.ThemeBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10, 12, 10)
        };

        _canvas = new Canvas { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        _yMaxLabel = new TextBlock { FontSize = 10, Foreground = UiFactory.ThemeBrush("TextSecondaryBrush"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _yMinLabel = new TextBlock { FontSize = 10, Foreground = UiFactory.ThemeBrush("TextSecondaryBrush"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };

        chartBox.Children.Add(_canvas);
        chartBox.Children.Add(_yMaxLabel);
        chartBox.Children.Add(_yMinLabel);

        mainStack.Children.Add(chartBox);
        Content = mainStack;

        _canvas.SizeChanged += (_, _) => Redraw();
    }

    private IList<double>? _trainData;
    private IList<double>? _valData;

    public void SetData(IList<double> trainData, IList<double> valData)
    {
        _trainData = trainData;
        _valData = valData;
        Redraw();
    }

    private void Redraw()
    {
        _canvas.Children.Clear();
        if (_trainData is null || _trainData.Count == 0)
        {
            _yMaxLabel.Text = "";
            _yMinLabel.Text = "";
            return;
        }

        var w = _canvas.ActualWidth;
        var h = _canvas.ActualHeight;
        if (w < 40 || h < 40) return;

        double padLeft = 40;
        double padRight = 15;
        double padTop = 15;
        double padBottom = 20;

        double plotW = Math.Max(10, w - padLeft - padRight);
        double plotH = Math.Max(10, h - padTop - padBottom);

        // Find min and max
        var allValues = _trainData.Concat(_valData ?? Enumerable.Empty<double>()).ToList();
        double minVal = allValues.Min();
        double maxVal = allValues.Max();

        if (Math.Abs(maxVal - minVal) < 1e-5)
        {
            maxVal += 0.1;
            minVal = Math.Max(0, minVal - 0.1);
        }

        _yMaxLabel.Text = $"{maxVal:F3}";
        _yMinLabel.Text = $"{minVal:F3}";

        // Draw horizontal grid lines
        var gridBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
        for (int i = 0; i <= 3; i++)
        {
            double y = padTop + (plotH / 3.0) * i;
            var line = new Line
            {
                X1 = padLeft,
                Y1 = y,
                X2 = padLeft + plotW,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            };
            _canvas.Children.Add(line);
        }

        // Draw Train line
        DrawSeries(_trainData, minVal, maxVal, padLeft, padTop, plotW, plotH, UiFactory.ThemeBrush("WasteMintBrush"));

        // Draw Val line
        if (_valData is not null && _valData.Count > 0)
        {
            DrawSeries(_valData, minVal, maxVal, padLeft, padTop, plotW, plotH, UiFactory.ThemeBrush("WasteCyanBrush"));
        }
    }

    private void DrawSeries(IList<double> data, double minVal, double maxVal, double padLeft, double padTop, double plotW, double plotH, Brush brush)
    {
        if (data.Count == 0) return;

        var points = new PointCollection();
        int count = data.Count;
        double stepX = count > 1 ? plotW / (count - 1) : plotW;

        for (int i = 0; i < count; i++)
        {
            double norm = (data[i] - minVal) / (maxVal - minVal);
            double x = count > 1 ? padLeft + i * stepX : padLeft + plotW / 2;
            double y = padTop + (1.0 - norm) * plotH;
            points.Add(new Point(x, y));

            // Point dot
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = brush,
                Margin = new Thickness(x - 3, y - 3, 0, 0)
            };
            _canvas.Children.Add(dot);
        }

        if (count > 1)
        {
            var polyline = new Polyline
            {
                Points = points,
                Stroke = brush,
                StrokeThickness = 2.5
            };
            _canvas.Children.Add(polyline);
        }
    }
}
