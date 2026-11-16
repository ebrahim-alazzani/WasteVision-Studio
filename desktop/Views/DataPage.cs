using System.IO;
using System.Text.Json.Nodes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WasteVision.Desktop.Common;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Views;

public sealed class DataPage : Page
{
    private readonly IWasteApiClient _api;
    private readonly Window _owner;
    private readonly TextBlock _statusInfo;

    public DataPage(IWasteApiClient api, Window owner)
    {
        _api = api;
        _owner = owner;

        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("البيانات والصور", 26, true));
        header.Children.Add(UiFactory.Label("ارفع مجموعة صور مصنفة في ملف ZIP أو اختر مجلداً محلياً يحتوي مجلدات لكل فئة.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        // Main Card
        var dropCard = UiFactory.Stack(14);
        dropCard.Children.Add(UiFactory.Label("تجهيز وفحص مجموعة البيانات", 17, true));
        dropCard.Children.Add(UiFactory.Label("يدعم النظام مجموعات TrashNet وتصنيفات النفايات المتعددة (كرتون، زجاج، معدن، ورق، بلاستيك، نفايات عامة).", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));

        _statusInfo = UiFactory.Label("لم يتم تحميل مجموعة صور بعد.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush"));

        var buttonsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        var btnZip = UiFactory.Action("اختيار ملف ZIP ورفعه", async (_, _) =>
        {
            try
            {
                var hWnd = WindowNative.GetWindowHandle(_owner);
                var zipPath = NativeFileDialog.PickZip(hWnd, @"C:\Users\ibral\Downloads");
                if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath)) return;

                _statusInfo.Text = $"جارٍ رفع وفحص صور الأرشيف ({Path.GetFileName(zipPath)})…";
                var result = await _api.UploadAsync("api/dataset/upload", zipPath);
                var dataset = result["dataset"];
                _statusInfo.Text = $"اكتمل الفحص: {dataset?["total_images"]} صورة صالحة عبر {dataset?["classes"]?.ToJsonString()}";
            }
            catch (Exception ex) { _statusInfo.Text = $"خطأ: {ex.Message}"; }
        });

        var btnFolder = UiFactory.Action("استيراد مجلد dataset-resized المحلي", async (_, _) =>
        {
            try
            {
                var localDatasetDir = @"E:\New folder (3)\CV_Waste_Sorting_Colab\data\dataset\dataset-resized";
                if (!Directory.Exists(localDatasetDir))
                {
                    localDatasetDir = @"E:\New folder (3)\CV_Waste_Sorting_Colab\data\dataset";
                }

                _statusInfo.Text = "جارٍ فحص المجلد واستيراد الفئات…";
                var result = await _api.PostAsync("api/dataset/import-folder", new { folder_path = localDatasetDir });
                var dataset = result["dataset"];
                _statusInfo.Text = $"تم الاستيراد: {dataset?["total_images"]} صورة في الفئات: {dataset?["classes"]?.ToJsonString()}";
            }
            catch (Exception ex) { _statusInfo.Text = $"خطأ: {ex.Message}"; }
        }, isPrimary: false);

        var btnRefresh = UiFactory.Action("تحديث فحص البيانات الحالية", async (_, _) =>
        {
            try
            {
                _statusInfo.Text = "جارٍ إعادة الفحص…";
                var res = await _api.PostAsync("api/dataset/refresh", new { });
                var dataset = res["dataset"];
                _statusInfo.Text = dataset is not null
                    ? $"البيانات الحالية: {dataset["total_images"]} صورة · {dataset["classes"]?.ToJsonString()}"
                    : "لا توجد مجموعة صور في مجلد data/dataset.";
            }
            catch (Exception ex) { _statusInfo.Text = ex.Message; }
        }, isPrimary: false);

        buttonsRow.Children.Add(btnZip);
        buttonsRow.Children.Add(btnFolder);
        buttonsRow.Children.Add(btnRefresh);

        dropCard.Children.Add(buttonsRow);
        dropCard.Children.Add(_statusInfo);
        body.Children.Add(UiFactory.Card(dropCard));

        // Guidelines
        var guideCard = UiFactory.Stack(8);
        guideCard.Children.Add(UiFactory.Label("إرشادات هيكل المجلدات المقبولة", 16, true));
        guideCard.Children.Add(UiFactory.Label("• يجب أن يحتوي المجلد على تصنيف لكل فئة كـ Sub-folder، مثل:\n   dataset/\n     ├── cardboard/\n     ├── glass/\n     ├── metal/\n     ├── paper/\n     ├── plastic/\n     └── trash/\n• يقبل النظام صيغ JPG, PNG, WEBP, BMP ويقوم OpenCV وPillow بتهيئتها إلى RGB.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(UiFactory.Card(guideCard));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        _ = LoadInitialStateAsync();
    }

    private async Task LoadInitialStateAsync()
    {
        try
        {
            var res = await _api.GetAsync("api/state");
            if (res["dataset"] is JsonObject ds)
            {
                _statusInfo.Text = $"المجموعة المكتشفة: {ds["total_images"]} صورة · الفئات: {ds["classes"]?.ToJsonString()} · تالفة: {ds["unreadable_images"] ?? 0}";
            }
        }
        catch (Exception ex) { _statusInfo.Text = ex.Message; }
    }
}
