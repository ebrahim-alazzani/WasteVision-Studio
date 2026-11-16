using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WasteVision.Desktop.Common;
using WasteVision.Desktop.Models;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Views;

public sealed class SettingsPage : Page
{
    private readonly INavigationService _nav;
    private readonly Hyperparameters _params;

    public SettingsPage(INavigationService nav, Hyperparameters parameters)
    {
        _nav = nav;
        _params = parameters;

        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("إعداد معاملات CNN", 26, true));
        header.Children.Add(UiFactory.Label("اضبط بارامترات الشبكة العصبية وخطة التدريب. تُحفظ الإعدادات وتُطبق على جلسة التدريب القادمة.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        // Grid of settings
        var grid = new Grid();
        for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 3; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var comboEpochs = Selector("عصور التدريب (Epochs)", new[] { "5", "8", "12", "15", "20", "30", "40" }, 2);
        var comboSize = Selector("أبعاد الصورة (Resolution)", new[] { "96", "128", "160", "224" }, 2);
        var comboBatch = Selector("حجم الدفعة (Batch Size)", new[] { "8", "16", "32", "64" }, 1);
        var comboFilters = Selector("مرشحات الالتفاف الأولى", new[] { "16", "32", "64" }, 1);
        var comboTest = Selector("نسبة الاختبار (Test %)", new[] { "10", "15", "20", "25" }, 1);
        var comboVal = Selector("نسبة التحقق (Val %)", new[] { "10", "15", "20", "25" }, 1);
        var comboDropout = Selector("معامل الإسقاط (Dropout)", new[] { "0.20", "0.35", "0.50" }, 1);
        var comboLr = Selector("معدل التعلم (Learning Rate)", new[] { "0.0001", "0.0003", "0.001", "0.003" }, 2);

        SetCell(comboEpochs, 0, 0);
        SetCell(comboSize, 0, 1);
        SetCell(comboBatch, 0, 2);
        SetCell(comboFilters, 1, 0);
        SetCell(comboTest, 1, 1);
        SetCell(comboVal, 1, 2);
        SetCell(comboDropout, 2, 0);
        SetCell(comboLr, 2, 1);

        grid.Children.Add(comboEpochs);
        grid.Children.Add(comboSize);
        grid.Children.Add(comboBatch);
        grid.Children.Add(comboFilters);
        grid.Children.Add(comboTest);
        grid.Children.Add(comboVal);
        grid.Children.Add(comboDropout);
        grid.Children.Add(comboLr);

        var chkAug = new CheckBox
        {
            Content = "تفعيل زيادة البيانات التلقائية (Random Flip, Rotation, Zoom)",
            IsChecked = _params.Augmentation,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var note = UiFactory.Label("الإعدادات الحالية متوازنة وموصى بها لبطاقات العرض ووحدات CPU العادية.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush"));

        var saveBtn = UiFactory.Action("حفظ المعاملات والانتقال إلى مختبر التدريب", (_, _) =>
        {
            _params.Epochs = int.Parse(Selected(comboEpochs, "12"));
            _params.ImageSize = int.Parse(Selected(comboSize, "160"));
            _params.BatchSize = int.Parse(Selected(comboBatch, "16"));
            _params.Filters = int.Parse(Selected(comboFilters, "32"));
            _params.TestPercent = int.Parse(Selected(comboTest, "15"));
            _params.ValidationPercent = int.Parse(Selected(comboVal, "15"));
            _params.Dropout = double.Parse(Selected(comboDropout, "0.35"), CultureInfo.InvariantCulture);
            _params.LearningRate = double.Parse(Selected(comboLr, "0.001"), CultureInfo.InvariantCulture);
            _params.Augmentation = chkAug.IsChecked == true;

            _nav.NavigateTo("training");
        });

        var panel = UiFactory.Stack();
        panel.Children.Add(grid);
        panel.Children.Add(chkAug);
        panel.Children.Add(note);
        panel.Children.Add(saveBtn);
        body.Children.Add(UiFactory.Card(panel));

        // Architecture description
        var pipeCard = UiFactory.Stack(6);
        pipeCard.Children.Add(UiFactory.Label("بنية شبكة CNN المطبقة", 16, true));
        pipeCard.Children.Add(UiFactory.Label("• Input (H × W × 3) → Data Augmentation → Rescaling(1/255)\n• Conv2D(F, 3x3) + Relu → MaxPool2D(2x2)\n• Conv2D(2F, 3x3) + Relu → MaxPool2D(2x2)\n• Conv2D(4F, 3x3) + Relu → MaxPool2D(2x2)\n• GlobalAveragePooling2D → Dropout → Dense(64) → Dense(NumClasses, Softmax)", 12, false, UiFactory.ThemeBrush("WasteMintBrush")));
        body.Children.Add(UiFactory.Card(pipeCard));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private static void SetCell(FrameworkElement element, int row, int col)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, col);
        element.Margin = new Thickness(6, 6, 6, 6);
    }

    private static ComboBox Selector(string title, string[] items, int selectedIndex)
    {
        var combo = new ComboBox { Header = title, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var item in items) combo.Items.Add(item);
        combo.SelectedIndex = selectedIndex;
        return combo;
    }

    private static string Selected(ComboBox combo, string fallback) => combo.SelectedItem?.ToString() ?? fallback;
}
