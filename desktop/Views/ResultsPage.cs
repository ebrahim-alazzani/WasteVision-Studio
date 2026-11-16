using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WasteVision.Desktop.Common;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Views;

public sealed class ResultsPage : Page
{
    private readonly IWasteApiClient _api;
    private readonly INavigationService _nav;
    private readonly TextBlock _status;
    private readonly StackPanel _summaryStack;
    private readonly StackPanel _chartsStack;
    private readonly StackPanel _matrixStack;
    private readonly StackPanel _perClassStack;
    private readonly CurveChartControl _lossChart;
    private readonly CurveChartControl _accuracyChart;

    public ResultsPage(IWasteApiClient api, INavigationService nav)
    {
        _api = api;
        _nav = nav;

        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("التقييم والمخططات البيانية", 26, true));
        header.Children.Add(UiFactory.Label("مؤشرات أداء النموذج ومنحنيات التعلم ومصفوفة الالتباس على مجموعة الاختبار المحجوزة.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        _status = UiFactory.Label("جارٍ تحميل نتائج التقييم…", 13);
        body.Children.Add(UiFactory.Card(_status));

        _summaryStack = UiFactory.Stack();
        body.Children.Add(UiFactory.Card(_summaryStack));

        // Charts Section
        _chartsStack = UiFactory.Stack(12);
        _chartsStack.Children.Add(UiFactory.Label("منحنيات التدريب والتحقق عبر العصور (Learning Curves)", 16, true));
        
        var chartsGrid = new Grid();
        chartsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        chartsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        chartsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _lossChart = new CurveChartControl("منحنى الخسارة (Loss Curve)", "خسارة التدريب", "خسارة التحقق");
        _accuracyChart = new CurveChartControl("منحنى الدقة (Accuracy Curve)", "دقة التدريب", "دقة التحقق");

        Grid.SetColumn(_lossChart, 0);
        Grid.SetColumn(_accuracyChart, 2);

        chartsGrid.Children.Add(_lossChart);
        chartsGrid.Children.Add(_accuracyChart);
        _chartsStack.Children.Add(chartsGrid);

        var btn3d = UiFactory.Action("عرض المحاكاة ثلاثية الأبعاد التفاعلية (3D Tensors & Sobel Surface)", (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("http://127.0.0.1:8765/assets/3d_visualizer.html") { UseShellExecute = true });
            }
            catch { }
        }, isPrimary: false);
        btn3d.HorizontalAlignment = HorizontalAlignment.Center;
        btn3d.Margin = new Thickness(0, 8, 0, 0);
        _chartsStack.Children.Add(btn3d);

        body.Children.Add(UiFactory.Card(_chartsStack));

        _matrixStack = UiFactory.Stack();
        body.Children.Add(UiFactory.Card(_matrixStack));

        _perClassStack = UiFactory.Stack();
        body.Children.Add(UiFactory.Card(_perClassStack));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        _ = LoadResultsAsync();
    }

    private async Task LoadResultsAsync()
    {
        try
        {
            var state = await _api.GetAsync("api/state");
            if (state["result"] is not JsonObject res)
            {
                _status.Text = "لا توجد نتائج محفوظة بعد. درّب النموذج أولاً لتوليد مصفوفة الالتباس والمقاييس.";
                return;
            }

            var acc = res["accuracy"]?.GetValue<double>() ?? 0;
            _status.Text = $"التقييم النهائي: دقة الاختبار الإجمالية {acc * 100:F1}% ({res["architecture"]})";

            // Load Curves from history
            if (state["history"] is JsonObject hist)
            {
                var loss = ExtractDoubleList(hist["loss"]);
                var valLoss = ExtractDoubleList(hist["val_loss"]);
                var accuracy = ExtractDoubleList(hist["accuracy"]);
                var valAccuracy = ExtractDoubleList(hist["val_accuracy"]);

                if (loss.Count > 0)
                {
                    _lossChart.SetData(loss, valLoss);
                }
                if (accuracy.Count > 0)
                {
                    _accuracyChart.SetData(accuracy, valAccuracy);
                }
            }

            // Summary Grid
            _summaryStack.Children.Clear();
            _summaryStack.Children.Add(UiFactory.Label("ملخص التقييم والبيانات", 16, true));
            var g = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            for (int i = 0; i < 4; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var m1 = UiFactory.KpiCard("دقة الاختبار", $"{acc * 100:F1}%", "Test Accuracy", "\uE8EC", UiFactory.ThemeBrush("WasteEmeraldBrush"));
            var m2 = UiFactory.KpiCard("عينات الاختبار", res["test_images"]?.ToString() ?? "—", "Test Split", "\uEB9F", UiFactory.ThemeBrush("WasteCyanBrush"));
            var m3 = UiFactory.KpiCard("عينات التدريب", res["train_images"]?.ToString() ?? "—", "Train Split", "\uE768", UiFactory.ThemeBrush("WasteMintBrush"));
            var m4 = UiFactory.KpiCard("العصور المنفذة", res["epochs_completed"]?.ToString() ?? "—", "Completed Epochs", "\uE713", UiFactory.ThemeBrush("WasteAmberBrush"));

            SetColMargin(m1, 0, 0, 0, 6, 0);
            SetColMargin(m2, 1, 6, 0, 6, 0);
            SetColMargin(m3, 2, 6, 0, 6, 0);
            SetColMargin(m4, 3, 6, 0, 0, 0);

            g.Children.Add(m1); g.Children.Add(m2); g.Children.Add(m3); g.Children.Add(m4);
            _summaryStack.Children.Add(g);

            // Confusion Matrix
            _matrixStack.Children.Clear();
            if (res["labels"] is JsonArray labels && res["confusion_matrix"] is JsonArray matrix)
            {
                _matrixStack.Children.Add(UiFactory.Label("مصفوفة الالتباس (Confusion Matrix)", 16, true));
                _matrixStack.Children.Add(UiFactory.Label("الصفوف تمثل الفئات الحقيقية، والأعمدة تمثل الفئات المتوقعة من النموذج.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush")));

                var table = new Grid { Margin = new Thickness(0, 10, 0, 0) };
                int n = labels.Count;
                table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                for (int i = 0; i < n; i++) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                for (int i = 0; i <= n; i++) table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var cornerCell = new Border
                {
                    Background = UiFactory.ThemeBrush("TileSoftBackgroundBrush"),
                    BorderBrush = UiFactory.ThemeBrush("CardBorderBrush"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 6, 6, 6),
                    Child = new TextBlock { Text = "الحقيقي / المتوقع", FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UiFactory.ThemeBrush("TextSecondaryBrush") }
                };
                Grid.SetRow(cornerCell, 0);
                Grid.SetColumn(cornerCell, 0);
                table.Children.Add(cornerCell);

                for (int col = 0; col < n; col++)
                {
                    var colHeader = new Border
                    {
                        Background = UiFactory.ThemeBrush("TileSoftBackgroundBrush"),
                        BorderBrush = UiFactory.ThemeBrush("CardBorderBrush"),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(6, 6, 6, 6),
                        Child = new TextBlock { Text = labels[col]?.ToString(), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UiFactory.ThemeBrush("TextPrimaryBrush") }
                    };
                    Grid.SetRow(colHeader, 0);
                    Grid.SetColumn(colHeader, col + 1);
                    table.Children.Add(colHeader);
                }

                for (int row = 0; row < n; row++)
                {
                    var rowHeader = new Border
                    {
                        Background = UiFactory.ThemeBrush("TileSoftBackgroundBrush"),
                        BorderBrush = UiFactory.ThemeBrush("CardBorderBrush"),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(6, 6, 6, 6),
                        Child = new TextBlock { Text = labels[row]?.ToString(), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UiFactory.ThemeBrush("TextPrimaryBrush") }
                    };
                    Grid.SetRow(rowHeader, row + 1);
                    Grid.SetColumn(rowHeader, 0);
                    table.Children.Add(rowHeader);

                    var matrixRow = matrix[row] as JsonArray;
                    for (int col = 0; col < n; col++)
                    {
                        var val = matrixRow?[col]?.GetValue<int>() ?? 0;
                        bool isDiagonal = row == col;

                        var cell = new Border
                        {
                            Background = isDiagonal && val > 0 ? UiFactory.ThemeBrush("TileSoftBackgroundBrush") : null,
                            BorderBrush = UiFactory.ThemeBrush("CardBorderBrush"),
                            BorderThickness = new Thickness(1),
                            Padding = new Thickness(6, 6, 6, 6),
                            Child = new TextBlock
                            {
                                Text = val.ToString(),
                                FontSize = 11,
                                FontWeight = isDiagonal ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                                Foreground = isDiagonal && val > 0 ? UiFactory.ThemeBrush("WasteMintBrush") : UiFactory.ThemeBrush("TextPrimaryBrush"),
                                HorizontalAlignment = HorizontalAlignment.Center
                            }
                        };
                        Grid.SetRow(cell, row + 1);
                        Grid.SetColumn(cell, col + 1);
                        table.Children.Add(cell);
                    }
                }

                _matrixStack.Children.Add(table);
            }

            // Per Class Metrics
            _perClassStack.Children.Clear();
            if (res["per_class"] is JsonArray perClass)
            {
                _perClassStack.Children.Add(UiFactory.Label("مقاييس الأداء لكل فئة منفصلة (Precision / Recall / F1)", 16, true));
                foreach (var item in perClass.OfType<JsonObject>())
                {
                    var lbl = item["label"]?.ToString() ?? "";
                    var prec = item["precision"]?.GetValue<double>() ?? 0;
                    var rec = item["recall"]?.GetValue<double>() ?? 0;
                    var f1 = item["f1"]?.GetValue<double>() ?? 0;
                    var supp = item["support"]?.GetValue<int>() ?? 0;

                    var row = UiFactory.Stack(4);
                    row.Margin = new Thickness(0, 4, 0, 4);

                    var top = new Grid();
                    top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                    var l = UiFactory.Label($"{lbl} (دعم: {supp} عينات)", 12, true);
                    var r = UiFactory.Label($"P: {prec * 100:F1}% · R: {rec * 100:F1}% · F1: {f1 * 100:F1}%", 11, false, UiFactory.ThemeBrush("WasteMintBrush"));

                    Grid.SetColumn(l, 0);
                    Grid.SetColumn(r, 1);
                    top.Children.Add(l); top.Children.Add(r);

                    var bar = new ProgressBar
                    {
                        Minimum = 0,
                        Maximum = 100,
                        Value = f1 * 100,
                        Height = 6,
                        CornerRadius = new CornerRadius(3),
                        Foreground = UiFactory.ThemeBrush("WasteCyanBrush")
                    };

                    row.Children.Add(top);
                    row.Children.Add(bar);
                    _perClassStack.Children.Add(row);
                }
            }
        }
        catch (Exception ex)
        {
            _status.Text = $"حدث خطأ أثناء تحميل النتائج: {ex.Message}";
        }
    }

    private static List<double> ExtractDoubleList(JsonNode? node)
    {
        var list = new List<double>();
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null && double.TryParse(item.ToString(), out var d))
                {
                    list.Add(d);
                }
            }
        }
        return list;
    }

    private static void SetColMargin(FrameworkElement el, int col, double l, double t, double r, double b)
    {
        Grid.SetColumn(el, col);
        el.Margin = new Thickness(l, t, r, b);
    }
}
