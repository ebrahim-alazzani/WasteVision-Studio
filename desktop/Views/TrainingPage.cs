using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WasteVision.Desktop.Common;
using WasteVision.Desktop.Models;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Views;

public sealed class TrainingPage : Page
{
    private readonly IWasteApiClient _api;
    private readonly INavigationService _nav;
    private readonly Hyperparameters _params;

    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly Border _bEpoch;
    private readonly Border _bLoss;
    private readonly Border _bValLoss;
    private readonly Border _bAcc;
    private readonly Button _trainBtn;
    private readonly Button _resetBtn;
    private readonly Button _viewResultsBtn;
    private bool _isMonitoring = false;

    public TrainingPage(IWasteApiClient api, INavigationService nav, Hyperparameters parameters)
    {
        _api = api;
        _nav = nav;
        _params = parameters;

        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("مختبر تدريب النموذج", 26, true));
        header.Children.Add(UiFactory.Label("ابدأ تدريب الشبكة العصبية CNN محلياً وتابع انخفاض الخسارة Loss وارتفاع الدقة Accuracy في الوقت الحقيقي دون انقطاع.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        _status = UiFactory.Label("جارٍ فحص حالة التدريب الحالية…", 13);
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 12, CornerRadius = new CornerRadius(6) };

        // Metrics live badges
        var metricsGrid = new Grid { Margin = new Thickness(0, 10, 0, 10) };
        for (int i = 0; i < 4; i++) metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _bEpoch = UiFactory.KpiCard("العصر الحالي", "0 / 0", "Epochs", "\uE768", UiFactory.ThemeBrush("WasteMintBrush"));
        _bLoss = UiFactory.KpiCard("خسارة التدريب", "—", "Loss", "\uE9D9", UiFactory.ThemeBrush("WasteAmberBrush"));
        _bValLoss = UiFactory.KpiCard("خسارة التحقق", "—", "Val Loss", "\uE9D9", UiFactory.ThemeBrush("WasteRoseBrush"));
        _bAcc = UiFactory.KpiCard("دقة التحقق", "—", "Val Accuracy", "\uE8EC", UiFactory.ThemeBrush("WasteEmeraldBrush"));

        SetColumnMargin(_bEpoch, 0, 0, 0, 6, 0);
        SetColumnMargin(_bLoss, 1, 6, 0, 6, 0);
        SetColumnMargin(_bValLoss, 2, 6, 0, 6, 0);
        SetColumnMargin(_bAcc, 3, 6, 0, 0, 0);

        metricsGrid.Children.Add(_bEpoch);
        metricsGrid.Children.Add(_bLoss);
        metricsGrid.Children.Add(_bValLoss);
        metricsGrid.Children.Add(_bAcc);

        _trainBtn = UiFactory.Action("بدء تدريب النموذج الآن", async (_, _) => await StartTrainingAsync());
        _resetBtn = UiFactory.Action("إعادة تعيين / فك القفل", async (_, _) => await ResetTrainingAsync(), isPrimary: false);
        _viewResultsBtn = UiFactory.Action("الانتقال إلى التقييم والمخططات", (_, _) => _nav.NavigateTo("results"), isPrimary: false);

        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 4, 0, 8) };
        actionsRow.Children.Add(_trainBtn);
        actionsRow.Children.Add(_resetBtn);
        actionsRow.Children.Add(_viewResultsBtn);

        var trainPanel = UiFactory.Stack(10);
        trainPanel.Children.Add(UiFactory.Label($"المعاملات النشطة: {_params.Epochs} عصر · أبعاد {_params.ImageSize}×{_params.ImageSize} · دفعة {_params.BatchSize} · فلاتر {_params.Filters} · معدل تعلم {_params.LearningRate}", 13, true));
        trainPanel.Children.Add(actionsRow);
        trainPanel.Children.Add(_progress);
        trainPanel.Children.Add(_status);
        trainPanel.Children.Add(metricsGrid);
        body.Children.Add(UiFactory.Card(trainPanel));

        var nextCard = UiFactory.Stack(6);
        nextCard.Children.Add(UiFactory.Label("استعراض المخططات التفاعلية والنتائج", 16, true));
        nextCard.Children.Add(UiFactory.Label("عند اكتمال التدريب يتم حفظ الأوزان المثلى تلقائياً عبر EarlyStopping، ويمكنك مراجعة تقرير الاختبار ومصفوفة الالتباس ورسوم المنحنيات من صفحة التقييم.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(UiFactory.Card(nextCard));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        Loaded += (_, _) => _ = RestoreCurrentStateAsync();
        _ = RestoreCurrentStateAsync();
    }

    private static void SetColumnMargin(FrameworkElement elem, int col, double l, double t, double r, double b)
    {
        Grid.SetColumn(elem, col);
        elem.Margin = new Thickness(l, t, r, b);
    }

    private async Task RestoreCurrentStateAsync()
    {
        try
        {
            var cur = await _api.GetAsync("api/progress");
            var phase = cur["status"]?.ToString() ?? "idle";
            var currentEpoch = cur["current_epoch"]?.GetValue<int>() ?? 0;
            var total = cur["total_epochs"]?.GetValue<int>() ?? 0;

            UpdateUiWithProgress(cur);

            if (phase == "running")
            {
                _trainBtn.IsEnabled = false;
                _status.Text = $"النموذج قيد التدريب حالياً في الخلفية · العصر {currentEpoch} من {total}";
                if (!_isMonitoring)
                {
                    _ = MonitorProgressAsync();
                }
            }
            else if (phase == "completed")
            {
                _trainBtn.IsEnabled = true;
                _status.Text = $"اكتمل التدريب السابق بنجاح ({currentEpoch} عصور). الأوزان جاهزة للاستدلال والتقييم.";
            }
            else if (phase == "failed")
            {
                _trainBtn.IsEnabled = true;
                _status.Text = $"تعذر التدريب السابق: {cur["error"]}";
            }
            else
            {
                _trainBtn.IsEnabled = true;
                _status.Text = "جاهز لبدء التدريب. اضغط الزر أعلاه للبدء.";
            }
        }
        catch (Exception ex)
        {
            _status.Text = $"تعذر جلب حالة التدريب: {ex.Message}";
        }
    }

    private async Task ResetTrainingAsync()
    {
        try
        {
            _status.Text = "جارٍ إعادة ضبط حالة التدريب وفك القفل…";
            await _api.PostAsync("api/train/reset", new { });
            _trainBtn.IsEnabled = true;
            _status.Text = "تمت إعادة ضبط حالة التدريب بنجاح. يمكنك الآن بدء تدريب جديد.";
            await RestoreCurrentStateAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"خطأ أثناء إعادة الضبط: {ex.Message}";
        }
    }

    private async Task StartTrainingAsync()
    {
        try
        {
            var datasetState = await _api.GetAsync("api/state");
            if (datasetState["dataset"] is null)
            {
                _status.Text = "تنبيه: يجب رفع أو فحص مجموعة البيانات أولاً من صفحة (البيانات والصور).";
                return;
            }

            _trainBtn.IsEnabled = false;
            _status.Text = "جارٍ بدء التدريب وتجهيز دفعات الصور…";

            await _api.PostAsync("api/train", _params.ToPayload());
            await MonitorProgressAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"تعذر بدء التدريب: {ex.Message}";
            _trainBtn.IsEnabled = true;
        }
    }

    private void UpdateUiWithProgress(JsonObject cur)
    {
        var currentEpoch = cur["current_epoch"]?.GetValue<int>() ?? 0;
        var total = cur["total_epochs"]?.GetValue<int>() ?? 0;
        if (total == 0 && cur["result"] is JsonObject r && r["epochs_completed"] != null)
        {
            total = r["epochs_completed"]?.GetValue<int>() ?? _params.Epochs;
        }
        if (total == 0) total = _params.Epochs;
        if (currentEpoch == 0 && total > 0 && cur["status"]?.ToString() == "completed")
        {
            currentEpoch = total;
        }

        _progress.Value = total > 0 ? Math.Min(100, currentEpoch * 100.0 / total) : 0;
        UiFactory.SetKpiValue(_bEpoch, $"{currentEpoch} / {total}");

        if (cur["history"] is JsonObject history)
        {
            var lossList = ExtractDoubles(history["loss"]);
            var valLossList = ExtractDoubles(history["val_loss"]);
            var valAccList = ExtractDoubles(history["val_accuracy"]);

            if (lossList.Count > 0)
                UiFactory.SetKpiValue(_bLoss, $"{lossList[^1]:F4}");
            if (valLossList.Count > 0)
                UiFactory.SetKpiValue(_bValLoss, $"{valLossList[^1]:F4}");
            if (valAccList.Count > 0)
                UiFactory.SetKpiValue(_bAcc, $"{valAccList[^1] * 100:F1}%");
        }
    }

    private static List<double> ExtractDoubles(JsonNode? node)
    {
        var list = new List<double>();
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item != null && double.TryParse(item.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var d))
                {
                    list.Add(d);
                }
            }
        }
        return list;
    }

    private async Task MonitorProgressAsync()
    {
        if (_isMonitoring) return;
        _isMonitoring = true;

        while (true)
        {
            try
            {
                var cur = await _api.GetAsync("api/progress");
                var currentEpoch = cur["current_epoch"]?.GetValue<int>() ?? 0;
                var total = cur["total_epochs"]?.GetValue<int>() ?? 0;
                var phase = cur["status"]?.ToString() ?? "idle";

                UpdateUiWithProgress(cur);

                _status.Text = phase switch
                {
                    "running" => $"النموذج يتدرب حالياً · العصر {currentEpoch} من {total} ({_progress.Value:F0}%)",
                    "completed" => "اكتمل التدريب بنجاح! تم حفظ النموذج وتقييم مجموعة الاختبار.",
                    "failed" => $"تعذر التدريب: {cur["error"]}",
                    _ => $"حالة التدريب: {phase}"
                };

                if (phase != "running")
                {
                    _trainBtn.IsEnabled = true;
                    _isMonitoring = false;
                    return;
                }
            }
            catch { }
            await Task.Delay(1000);
        }
    }
}
