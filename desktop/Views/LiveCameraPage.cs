using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WasteVision.Desktop.Common;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Views;

public sealed class LiveCameraPage : Page
{
    private readonly IWasteApiClient _api;
    private readonly Window _owner;
    private readonly TextBlock _cameraStatus;
    private Process? _cameraProcess;

    public LiveCameraPage(IWasteApiClient api, Window owner)
    {
        _api = api;
        _owner = owner;

        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("كاميرا الفرز الذكي المباشر (Live Computer Vision)", 26, true));
        header.Children.Add(UiFactory.Label("توجيه كاميرا الويب في الوقت الحقيقي لتصنيف النفايات فورياً عبر إطارات OpenCV ونموذج CNN.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        // Main Action Card
        var mainCard = UiFactory.Stack(14);
        mainCard.Children.Add(UiFactory.Label("تشغيل معالجة الفيديو المباشر في الوقت الحقيقي", 17, true));
        mainCard.Children.Add(UiFactory.Label("تقوم كاميرا OpenCV بفتح بث الفيديو من الكاميرا وتطبيق شبكة CNN على كل إطار (Frame) مع رسم مستطيل استهداف ذكي، وعرض الفئة ونسبة الثقة ومعدل الإطارات (FPS).", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));

        _cameraStatus = UiFactory.Label("الكاميرا جاهزة للتشغيل.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush"));

        var buttonsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 6, 0, 0) };

        var btnStart = UiFactory.Action("تشغيل كاميرا OpenCV المباشرة (Live Stream)", (_, _) => StartOpenCvCamera());
        var btnStop = UiFactory.Action("إيقاف الكاميرا", (_, _) => StopOpenCvCamera(), isPrimary: false);
        var btnWebCamera = UiFactory.Action("فتح كاميرا المتصفح (WebRTC HUD)", (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("http://127.0.0.1:8765/#page-camera") { UseShellExecute = true });
            }
            catch { }
        }, isPrimary: false);

        buttonsRow.Children.Add(btnStart);
        buttonsRow.Children.Add(btnStop);
        buttonsRow.Children.Add(btnWebCamera);

        mainCard.Children.Add(buttonsRow);
        mainCard.Children.Add(_cameraStatus);
        body.Children.Add(UiFactory.Card(mainCard));

        // Instructions Card
        var infoCard = UiFactory.Stack(10);
        infoCard.Children.Add(UiFactory.Label("إرشادات استخدام كاميرا الفرز الذكي", 16, true));
        infoCard.Children.Add(UiFactory.Label("1. تأكد من توصيل كاميرا الويب وتوجيهها نحو بيئة إضاءة مناسبة.\n2. ضع عنصر النفاية (علبة بلاستيك، كرتون، زجاج، ورق، معدن) داخل المربع المركزي في نافذة الكاميرا.\n3. يقوم النموذج بتوقع الصنف فورياً بنسبة الثقة، وتلوين الإطار بلون مميز لكل فئة.\n4. اختصارات لوحة المفاتيح أثناء تشغيل نافذة OpenCV:\n   • 'q' أو Esc: إغلاق الكاميرا.\n   • 's': التقاط صورة وحفظها في مجلد data/ كلقطة تدريب جديدة.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(UiFactory.Card(infoCard));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private void StartOpenCvCamera()
    {
        try
        {
            if (_cameraProcess != null && !_cameraProcess.HasExited)
            {
                _cameraStatus.Text = "الكاميرا قيد التشغيل بالفعل في نافذة مستقلة.";
                return;
            }

            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
            var batPath = Path.Combine(root, "run_camera.bat");

            if (!File.Exists(batPath))
            {
                batPath = @"E:\New folder (3)\CV_Waste_Sorting_Colab\run_camera.bat";
            }

            _cameraProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = batPath,
                    WorkingDirectory = root,
                    UseShellExecute = true
                }
            };
            _cameraProcess.Start();
            _cameraStatus.Text = "تم إطلاق نافذة كاميرا OpenCV بنجاح. وجّه الكاميرا نحو النفايات.";
        }
        catch (Exception ex)
        {
            _cameraStatus.Text = $"تعذر تشغيل الكاميرا: {ex.Message}";
        }
    }

    private void StopOpenCvCamera()
    {
        try
        {
            if (_cameraProcess != null && !_cameraProcess.HasExited)
            {
                _cameraProcess.Kill(true);
                _cameraProcess = null;
                _cameraStatus.Text = "تم إيقاف كاميرا OpenCV بنجاح.";
            }
            else
            {
                _cameraStatus.Text = "الكاميرا متوقفة بالفعل.";
            }
        }
        catch (Exception ex)
        {
            _cameraStatus.Text = $"خطأ أثناء الإيقاف: {ex.Message}";
        }
    }
}
