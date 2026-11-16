using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WasteVision.Desktop.Common;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Views;

public sealed class OverviewPage : Page
{
    private readonly IWasteApiClient _api;
    private readonly INavigationService _nav;
    private readonly Border _kpiImages;
    private readonly Border _kpiClasses;
    private readonly Border _kpiModel;
    private readonly Border _kpiAccuracy;

    public OverviewPage(IWasteApiClient api, INavigationService nav)
    {
        _api = api;
        _nav = nav;

        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("لوحة التحكم العامة", 26, true));
        header.Children.Add(UiFactory.Label("نظام ذكي متكامل لفرز وتصنيف النفايات بصرياً بالتعلم العميق والرؤية الحاسوبية.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        // Hero Card
        var heroActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        heroActions.Children.Add(UiFactory.Action("تجهيز مجموعة الصور", (_, _) => _nav.NavigateTo("data")));
        heroActions.Children.Add(UiFactory.Action("ضبط وبدء التدريب", (_, _) => _nav.NavigateTo("training"), isPrimary: false));
        heroActions.Children.Add(UiFactory.Action("اختبار صورة جديدة", (_, _) => _nav.NavigateTo("predict"), isPrimary: false));

        var heroStack = UiFactory.Stack(8);
        heroStack.Children.Add(UiFactory.Label("WINUI 3 · FASTAPI · TENSORFLOW · OPENCV", 11, true, UiFactory.ThemeBrush("WasteMintBrush")));
        heroStack.Children.Add(UiFactory.Label("من الصورة إلى الفئة: فرز النفايات بالذكاء الاصطناعي", 22, true));
        heroStack.Children.Add(UiFactory.Label("منصة متكاملة لتدريب شبكات CNN الالتفافية على فئات المخلفات، وتقييمها، والاستدلال الفوري بنقرة زر.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        heroStack.Children.Add(heroActions);
        body.Children.Add(UiFactory.Card(heroStack, new Thickness(24)));

        // KPI Grid
        var kpiGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        for (int i = 0; i < 4; i++) kpiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _kpiImages = UiFactory.KpiCard("إجمالي الصور الصالحة", "—", "جارٍ الفحص", "\uEB9F", UiFactory.ThemeBrush("WasteCyanBrush"));
        _kpiClasses = UiFactory.KpiCard("فئات التصنيف", "—", "مجلد لكل فئة", "\uE8EC", UiFactory.ThemeBrush("WasteMintBrush"));
        _kpiModel = UiFactory.KpiCard("حالة تدريب CNN", "جاهز", "خدمة Python متصلة", "\uE768", UiFactory.ThemeBrush("WasteAmberBrush"));
        _kpiAccuracy = UiFactory.KpiCard("دقة الاختبار", "—", "تقييم مستقل", "\uE9D9", UiFactory.ThemeBrush("WasteEmeraldBrush"));

        SetColumnMargin(_kpiImages, 0, 0, 0, 6, 0);
        SetColumnMargin(_kpiClasses, 1, 6, 0, 6, 0);
        SetColumnMargin(_kpiModel, 2, 6, 0, 6, 0);
        SetColumnMargin(_kpiAccuracy, 3, 6, 0, 0, 0);

        kpiGrid.Children.Add(_kpiImages);
        kpiGrid.Children.Add(_kpiClasses);
        kpiGrid.Children.Add(_kpiModel);
        kpiGrid.Children.Add(_kpiAccuracy);
        body.Children.Add(kpiGrid);

        // Workflow Steps
        var flowTitle = UiFactory.Label("خطوات العمل المنظمة في المختبر", 16, true);
        var workflowGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        for (int i = 0; i < 4; i++) workflowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var s1 = WorkflowTile("01 · جهّز البيانات", "ارفع ملف ZIP أو اختر مجلدًا مصنفاً.", () => _nav.NavigateTo("data"));
        var s2 = WorkflowTile("02 · اضبط المعاملات", "حدد العصور، وحجم الصورة، والدفعة.", () => _nav.NavigateTo("settings"));
        var s3 = WorkflowTile("03 · درّب CNN", "شاهد تحديث العصور ومقاييس الشبكة.", () => _nav.NavigateTo("training"));
        var s4 = WorkflowTile("04 · افحص النتائج", "راجع مصفوفة الالتباس ودقة كل فئة.", () => _nav.NavigateTo("results"));

        SetColumnMargin(s1, 0, 0, 0, 6, 0);
        SetColumnMargin(s2, 1, 6, 0, 6, 0);
        SetColumnMargin(s3, 2, 6, 0, 6, 0);
        SetColumnMargin(s4, 3, 6, 0, 0, 0);

        workflowGrid.Children.Add(s1);
        workflowGrid.Children.Add(s2);
        workflowGrid.Children.Add(s3);
        workflowGrid.Children.Add(s4);

        var flowStack = UiFactory.Stack();
        flowStack.Children.Add(flowTitle);
        flowStack.Children.Add(workflowGrid);
        body.Children.Add(UiFactory.Card(flowStack));

        // Tech specs
        var archStack = UiFactory.Stack(8);
        archStack.Children.Add(UiFactory.Label("المعمارية النظيفة للمشروع (SOLID & Clean Architecture)", 16, true));
        archStack.Children.Add(UiFactory.Label("• فصل تام للمسؤوليات (SRP) عبر كلاسات مستقلة لكل شاشة وخدمة.\n• واجهة Windows أصلية بـ WinUI 3 وخدمة FastAPI محلية تتشاركان النموذج نفسه.\n• شبكة عصبية التفافية CNN مع حماية التوزيع الطبقي Stratified Split.\n• التزام بأسس الحماية البيئية ومنع تلوث الموائل الطبيعية وإيذاء الكائنات الحية.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(UiFactory.Card(archStack));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        _ = LoadDataAsync();
    }

    private static void SetColumnMargin(FrameworkElement elem, int col, double l, double t, double r, double b)
    {
        Grid.SetColumn(elem, col);
        elem.Margin = new Thickness(l, t, r, b);
    }

    private static Border WorkflowTile(string title, string desc, Action onClick)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = UiFactory.ThemeBrush("WasteMintBrush") });
        stack.Children.Add(new TextBlock { Text = desc, FontSize = 11, Foreground = UiFactory.ThemeBrush("TextSecondaryBrush"), TextWrapping = TextWrapping.Wrap });

        var border = new Border
        {
            Child = stack,
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(10),
            Background = UiFactory.ThemeBrush("TileSoftBackgroundBrush"),
            BorderBrush = UiFactory.ThemeBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1)
        };
        border.PointerPressed += (_, _) => onClick();
        return border;
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var state = await _api.GetAsync("api/state");
            var ds = state["dataset"];
            var training = state["training"];
            var result = state["result"];

            if (ds is not null)
            {
                UiFactory.SetKpiValue(_kpiImages, ds["total_images"]?.ToString() ?? "—");
                if (ds["classes"] is JsonArray classes)
                    UiFactory.SetKpiValue(_kpiClasses, $"{classes.Count} فئات");
            }
            if (training is not null)
            {
                var st = training["status"]?.ToString() ?? "idle";
                UiFactory.SetKpiValue(_kpiModel, st == "completed" ? "مكتمل" : st == "running" ? "قيد التدريب" : "جاهز");
            }
            if (result is not null && result["accuracy"]?.GetValue<double>() is double acc)
            {
                UiFactory.SetKpiValue(_kpiAccuracy, $"{acc * 100:F1}%");
            }
        }
        catch { }
    }
}
