using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WasteVision.Desktop.Common;

namespace WasteVision.Desktop.Views;

public sealed class AboutPage : Page
{
    public AboutPage()
    {
        var body = UiFactory.Stack(18);

        // Header
        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 8) };
        header.Children.Add(UiFactory.Label("عن المشروع والتوثيق الأكاديمي", 26, true));
        header.Children.Add(UiFactory.Label("معلومات تقنية شاملة حول مشروع WasteVision Studio ونماذج فرز النفايات.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(header);

        // Arch card
        var info = UiFactory.Stack(10);
        info.Children.Add(UiFactory.Label("المعمارية المتكاملة والتقنيات المستخدمة", 18, true));
        info.Children.Add(UiFactory.Label("• واجهة Windows أصلية بـ WinUI 3 وWindows App SDK معمارية Unpacked على .NET 9.\n• واجهة ويب حديثة ومتجاوبة مستوحاة من تنظيم ونظافة MediScript AI.\n• خدمة خلفية موحدة بـ FastAPI تعمل على 127.0.0.1:8765 تشترك فيها الواجهتان.\n• نماذج تعلم عميق باستخدام TensorFlow وKeras مع طبقات Convolution وMaxPooling وDropout.\n• مكتبة OpenCV وPillow لقراءة ومعالجة وتوحيد أبعاد الصور.", 13, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(UiFactory.Card(info));

        // Developer card
        var dev = UiFactory.Stack(8);
        dev.Children.Add(UiFactory.Label("المطور والمستودع البرمجي", 16, true));
        dev.Children.Add(UiFactory.Label("• المطور: إبراهيم العزاني (Ebrahim Alazzani)\n• GitHub: https://github.com/ebrahim-alazzani/WasteVision-Studio\n• الترخيص: MIT License مفتوح المصدر للبحث والتطوير الأكاديمي.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(UiFactory.Card(dev));

        // Safety card
        var safety = UiFactory.Stack(8);
        safety.Children.Add(UiFactory.Label("الأثر البيئي وحماية الكائنات الحية والحيوانات", 16, true));
        safety.Children.Add(UiFactory.Label("يشكل الخلط العشوائي للمخلفات البلاستيكية الحادة والمواد الطبية والعلب المعدنية خطراً جسيماً على الطيور والحيوانات السائبة عبر الابتلاع والاختناق والتسمم. يسهم الفرز الذكي الدقيق عند المصدر في تقليل وصول هذه المواد للبيئة البرية والبحرية، وتحسين دورة الاقتصاد الدائري.", 12, false, UiFactory.ThemeBrush("TextSecondaryBrush")));
        body.Children.Add(UiFactory.Card(safety));

        Content = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(24, 16, 24, 32), Child = body },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }
}
