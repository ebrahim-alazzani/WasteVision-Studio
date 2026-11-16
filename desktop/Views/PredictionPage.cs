using System.IO;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT.Interop;
using WasteVision.Desktop.Common;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Views;

public sealed class PredictionPage : Page
{
    private readonly IWasteApiClient _api;
    private readonly Window _owner;

    private readonly Image _previewImage;
    private readonly StackPanel _resultHero;
    private readonly TextBlock _predictedClassLabel;
    private readonly TextBlock _confidenceLabel;
    private readonly StackPanel _probList;
    private readonly TextBlock _status;
    private readonly Border _disposalTipCard;
    private readonly TextBlock _disposalTipText;

    public PredictionPage(IWasteApiClient api, Window owner)
    {
        _api = api;
        _owner = owner;

        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("اختبار صورة واستدلال فوري", 26, true));
        header.Children.Add(UiFactory.Label("اختر أي صورة نفاية من جهازك للتنبؤ بفئتها ونسبة الثقة بناءً على النموذج المدرب.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        _previewImage = new Image
        {
            MaxHeight = 260,
            MaxWidth = 360,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 10, 0, 14)
        };

        _resultHero = UiFactory.Stack(10);
        _resultHero.Visibility = Visibility.Collapsed;

        _predictedClassLabel = new TextBlock
        {
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = UiFactory.ThemeBrush("WasteMintBrush")
        };
        _confidenceLabel = new TextBlock
        {
            FontSize = 14,
            Foreground = UiFactory.ThemeBrush("TextSecondaryBrush")
        };
        _probList = UiFactory.Stack(6);

        // Eco tip card
        _disposalTipCard = new Border
        {
            Background = UiFactory.ThemeBrush("TileSoftBackgroundBrush"),
            BorderBrush = UiFactory.ThemeBrush("WasteMintBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 8, 0, 0)
        };
        _disposalTipText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiFactory.ThemeBrush("TextPrimaryBrush")
        };
        _disposalTipCard.Child = _disposalTipText;

        _resultHero.Children.Add(_predictedClassLabel);
        _resultHero.Children.Add(_confidenceLabel);
        _resultHero.Children.Add(_disposalTipCard);
        _resultHero.Children.Add(_probList);

        _status = UiFactory.Label("اختر صورة للبدء في الاستدلال، أو اختر من العينات الجاهزة أدناه.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush"));

        var pickAndPredictBtn = UiFactory.Action("اختيار صورة وتصنيفها فورياً", async (_, _) => await PickAndPredictAsync());

        // Quick sample test buttons
        var samplesRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        samplesRow.Children.Add(UiFactory.Label("عينات سريعة للاختبار:", 12, true));
        
        var sampleCardboard = UiFactory.Action("كرتون", async (_, _) => await PredictSampleAsync("cardboard"), isPrimary: false);
        var samplePlastic = UiFactory.Action("بلاستيك", async (_, _) => await PredictSampleAsync("plastic"), isPrimary: false);
        var sampleGlass = UiFactory.Action("زجاج", async (_, _) => await PredictSampleAsync("glass"), isPrimary: false);
        var sampleMetal = UiFactory.Action("معدن", async (_, _) => await PredictSampleAsync("metal"), isPrimary: false);
        
        samplesRow.Children.Add(sampleCardboard);
        samplesRow.Children.Add(samplePlastic);
        samplesRow.Children.Add(sampleGlass);
        samplesRow.Children.Add(sampleMetal);

        var panel = UiFactory.Stack(12);
        panel.Children.Add(pickAndPredictBtn);
        panel.Children.Add(samplesRow);
        panel.Children.Add(_previewImage);
        panel.Children.Add(_status);
        panel.Children.Add(_resultHero);
        body.Children.Add(UiFactory.Card(panel));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private async Task PickAndPredictAsync()
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(_owner);
            var initialDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\data\dataset\dataset-resized");
            if (!Directory.Exists(initialDir))
            {
                initialDir = @"E:\New folder (3)\CV_Waste_Sorting_Colab\data\dataset\dataset-resized";
            }

            var filePath = NativeFileDialog.PickImage(hWnd, Directory.Exists(initialDir) ? initialDir : null);
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

            await ProcessImageFileAsync(filePath);
        }
        catch (Exception ex)
        {
            _status.Text = $"تعذر اختيار أو فحص الصورة: {ex.Message}";
            _resultHero.Visibility = Visibility.Collapsed;
        }
    }

    private async Task PredictSampleAsync(string category)
    {
        var sampleDir = Path.Combine(@"E:\New folder (3)\CV_Waste_Sorting_Colab\data\dataset\dataset-resized", category);
        if (Directory.Exists(sampleDir))
        {
            var files = Directory.GetFiles(sampleDir, "*.jpg");
            if (files.Length > 0)
            {
                await ProcessImageFileAsync(files[0]);
                return;
            }
        }
        _status.Text = $"لم يتم العثور على عينة في مجلد {category}. يرجى اختيار ملف يدوياً.";
    }

    private async Task ProcessImageFileAsync(string filePath)
    {
        try
        {
            // Preview Image
            var bitmap = new BitmapImage(new Uri(filePath));
            _previewImage.Source = bitmap;
            _previewImage.Visibility = Visibility.Visible;

            _status.Text = $"جارٍ تحليل الصورة بالشبكة العصبية ({Path.GetFileName(filePath)})…";
            var result = await _api.UploadAsync("api/predict", filePath);

            var pred = result["prediction"]?.ToString() ?? "";
            var conf = result["confidence"]?.GetValue<double>() ?? 0;
            _predictedClassLabel.Text = $"الفئة المتوقعة: {pred.ToUpperInvariant()}";
            _confidenceLabel.Text = $"نسبة ثقة النموذج: {conf * 100:F1}%";

            // Eco Tip
            var tip = GetDisposalGuidance(pred);
            _disposalTipText.Text = $"💡 إرشادات الفرز البيئي: {tip}";

            _probList.Children.Clear();
            if (result["probabilities"] is JsonArray arr)
            {
                _probList.Children.Add(UiFactory.Label("توزيع احتمالات الفئات الست:", 13, true));
                foreach (var p in arr.OfType<JsonObject>())
                {
                    var lbl = p["label"]?.ToString() ?? "";
                    var prob = p["probability"]?.GetValue<double>() ?? 0;

                    var rowGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });

                    var bar = new ProgressBar
                    {
                        Minimum = 0,
                        Maximum = 100,
                        Value = prob * 100,
                        Height = 8,
                        CornerRadius = new CornerRadius(4),
                        Foreground = lbl == pred ? UiFactory.ThemeBrush("WasteMintBrush") : UiFactory.ThemeBrush("WasteCyanBrush")
                    };
                    var tLbl = new TextBlock { Text = lbl, FontSize = 12, FontWeight = lbl == pred ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal, Foreground = UiFactory.ThemeBrush("TextPrimaryBrush") };
                    var tPct = new TextBlock { Text = $"{prob * 100:F1}%", FontSize = 12, Foreground = UiFactory.ThemeBrush("TextSecondaryBrush"), HorizontalAlignment = HorizontalAlignment.Right };

                    Grid.SetColumn(tLbl, 0);
                    Grid.SetColumn(bar, 1);
                    Grid.SetColumn(tPct, 2);

                    rowGrid.Children.Add(tLbl);
                    rowGrid.Children.Add(bar);
                    rowGrid.Children.Add(tPct);
                    _probList.Children.Add(rowGrid);
                }
            }

            _resultHero.Visibility = Visibility.Visible;
            _status.Text = "تم تصنيف الصورة بنجاح عبر الشبكة العصبية.";
        }
        catch (Exception ex)
        {
            _status.Text = $"تعذر الاستدلال: {ex.Message}";
            _resultHero.Visibility = Visibility.Collapsed;
        }
    }

    private static string GetDisposalGuidance(string category) => category.ToLowerInvariant() switch
    {
        "cardboard" => "كرتون قابل للتدوير. يُرجى طي الصندوق وتفريغه ووضعه في حاوية الورق والكرتون الجافة لحماية الموارد وتقليل الحجم.",
        "glass" => "زجاج قابل للتدوير 100%. يُغسل بالماء ويوضع بحذر في حاوية الزجاج لتجنب كسر الحواف وتأمين سلامة عمال النظافة.",
        "metal" => "معادن وعلب ألومنيوم/قصدير. يُفضل ضغط العلب وغسلها قبل وضعها في حاوية المعادن لإعادة صهرها وتوفير 95% من طاقة التعدين.",
        "paper" => "ورق نظيف قابل لإعادة التدوير. يُمنع خلطه بالسوائل أو الزيوت ويوضع في سلة إعادة تدوير الورق.",
        "plastic" => "بلاستيك صناعي قابل للتدوير. يُشطف للتخلص من الرواسب ويوضع في حاوية البلاستيك لحماية البيئة والحيوانات من التلوث البلاستيكي.",
        "trash" => "نفايات عامة غير قابلة للتدوير المنزلي. تُحكم في أكياس مخصصة لنقلها للمدافن الصحية المعتمدة بأمان بيئي.",
        _ => "يُرجى فرز المواد الجافة عن الرطبة ووضع المواد القابلة لإعادة التدوير في الحاويات المخصصة."
    };
}
