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
    private Button? _trainBtn;

    public TrainingPage(IWasteApiClient api, INavigationService nav, Hyperparameters parameters)
    {
        _api = api;
        _nav = nav;
        _params = parameters;

        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("مختبر تدريب النموذج", 26, true));
        header.Children.Add(UiFactory.Label("ابدأ تدريب الشبكة العصبية CNN محلياً وتابع انخفاض الخسارة Loss وارتفاع الدقة Accuracy في الوقت الحقيقي.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        _status = UiFactory.Label("ارفع البيانات أولاً ثم اضغط بدء التدريب.", 13);
        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 12, CornerRadius = new CornerRadius(6) };

        // Metrics live badges
        var metricsGrid = new Grid { Margin = new Thickness(0, 10, 0, 10) };
        for (int i = 0; i < 4; i++) metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _bEpoch = UiFactory.KpiCard("العصر الحالي", "0 / 0", "Epochs", "&#xE768;", UiFactory.ThemeBrush("WasteMintBrush"));
        _bLoss = UiFactory.KpiCard("خسارة التدريب", "—", "Loss", "&#xE9D9;", UiFactory.ThemeBrush("WasteAmberBrush"));
        _bValLoss = UiFactory.KpiCard("خسارة التحقق", "—", "Val Loss", "&#xE9D9;", UiFactory.ThemeBrush("WasteRoseBrush"));
        _bAcc = UiFactory.KpiCard("دقة التحقق", "—", "Val Accuracy", "&#xE8EC;", UiFactory.ThemeBrush("WasteEmeraldBrush"));

        SetColumnMargin(_bEpoch, 0, 0, 0, 6, 0);
        SetColumnMargin(_bLoss, 1, 6, 0, 6, 0);
        SetColumnMargin(_bValLoss, 2, 6, 0, 6, 0);
        SetColumnMargin(_bAcc, 3, 6, 0, 0, 0);

        metricsGrid.Children.Add(_bEpoch);
        metricsGrid.Children.Add(_bLoss);
        metricsGrid.Children.Add(_bValLoss);
        metricsGrid.Children.Add(_bAcc);

        _trainBtn = UiFactory.Action("بدء تدريب النموذج الآن", async (_, _) => await StartTrainingAsync());

        var trainPanel = UiFactory.Stack();
        trainPanel.Children.Add(UiFactory.Label($"المعاملات النشطة: {_params.Epochs} عصر · أبعاد {_params.ImageSize}×{_params.ImageSize} · دفعة {_params.BatchSize} · فلاتر {_params.Filters} · معدل تعلم {_params.LearningRate}", 13, true));
        trainPanel.Children.Add(_trainBtn);
        trainPanel.Children.Add(_progress);
        trainPanel.Children.Add(_status);
        trainPanel.Children.Add(metricsGrid);
        body.Children.Add(UiFactory.Card(trainPanel));

        var nextCard = UiFactory.Stack(6);
        nextCard.Children.Add(UiFactory.Label("استعراض المخططات التفاعلية", 16, true));
        nextCard.Children.Add(UiFactory.Label("عند اكتمال التدريب يتم حفظ الأوزان المثلى تلقائياً عبر EarlyStopping، ويمكنك مراجعة تقرير الاختبار ومصفوفة الالتباس من صفحة التقييم.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        nextCard.Children.Add(UiFactory.Action("الانتقال إلى التقييم والمخططات", (_, _) => _nav.NavigateTo("results"), isPrimary: false));
        body.Children.Add(UiFactory.Card(nextCard));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static void SetColumnMargin(FrameworkElement elem, int col, double l, double t, double r, double b)
    {
        Grid.SetColumn(elem, col);
        elem.Margin = new Thickness(l, t, r, b);
    }

    private async Task StartTrainingAsync()
    {
        try
        {
            var datasetState = await _api.GetAsync("api/state");
            if (datasetState["dataset"] is null)
            {
                _status.Text = "تنبيه: يجب رفع مجموعة البيانات أولاً من صفحة (البيانات والصور).";
                return;
            }
            if (_trainBtn != null) _trainBtn.IsEnabled = false;
            _status.Text = "جارٍ بدء التدريب وتجهيز دفعات الصور…";

            await _api.PostAsync("api/train", _params.ToPayload());
            await MonitorProgressAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"تعذر بدء التدريب: {ex.Message}";
            if (_trainBtn != null) _trainBtn.IsEnabled = true;
        }
    }

    private async Task MonitorProgressAsync()
    {
        while (true)
        {
            try
            {
                var cur = await _api.GetAsync("api/progress");
                var currentEpoch = cur["current_epoch"]?.GetValue<int>() ?? 0;
                var total = cur["total_epochs"]?.GetValue<int>() ?? 0;
                var phase = cur["status"]?.ToString() ?? "idle";

                _progress.Value = total > 0 ? Math.Min(100, currentEpoch * 100.0 / total) : 0;
                UiFactory.SetKpiValue(_bEpoch, $"{currentEpoch} / {total}");

                if (cur["history"] is JsonObject history)
                {
                    if (history["loss"] is JsonArray lossArr && lossArr.Count > 0 && lossArr.Last()?.GetValue<double>() is double l)
                        UiFactory.SetKpiValue(_bLoss, $"{l:F4}");
                    if (history["val_loss"] is JsonArray valLossArr && valLossArr.Count > 0 && valLossArr.Last()?.GetValue<double>() is double vl)
                        UiFactory.SetKpiValue(_bValLoss, $"{vl:F4}");
                    if (history["val_accuracy"] is JsonArray valAccArr && valAccArr.Count > 0 && valAccArr.Last()?.GetValue<double>() is double va)
                        UiFactory.SetKpiValue(_bAcc, $"{va * 100:F1}%");
                }

                _status.Text = phase switch
                {
                    "running" => $"النموذج يتدرب حالياً · العصر {currentEpoch} من {total} ({_progress.Value:F0}%)",
                    "completed" => "اكتمل التدريب بنجاح! تم حفظ النموذج وتقييم مجموعة الاختبار.",
                    "failed" => $"تعذر التدريب: {cur["error"]}",
                    _ => $"حالة التدريب: {phase}"
                };

                if (phase != "running")
                {
                    if (_trainBtn != null) _trainBtn.IsEnabled = true;
                    if (phase == "completed")
                    {
                        await Task.Delay(1500);
                        _nav.NavigateTo("results");
                    }
                    return;
                }
            }
            catch { }
            await Task.Delay(1000);
        }
    }
}
