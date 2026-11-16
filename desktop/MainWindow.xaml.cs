using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using WasteVision.Desktop.Services;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly IWasteApiClient _api;
    private readonly ILocalApiHost _localApi;
    private readonly INavigationService _nav;

    public MainWindow()
    {
        InitializeComponent();
        Title = "WasteVision Studio · مختبر الفرز الذكي بالرؤية الحاسوبية";

        ConfigureSeamlessTitleBarAndIcon();

        _api = new WasteApiClient();
        _localApi = new LocalApiHost();
        _nav = new NavigationService(ContentFrame, _api, this, UpdateNavSelection);

        Closed += (_, _) =>
        {
            _localApi.Dispose();
            _api.Dispose();
        };

        _nav.NavigateTo("overview");
        _ = ConnectBackendAsync();
    }

    private void ConfigureSeamlessTitleBarAndIcon()
    {
        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = appWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 230, 240, 245);
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(40, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(70, 255, 255, 255);
            }

            // Set Application Icon in TitleBar and Taskbar
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (!File.Exists(iconPath))
            {
                iconPath = @"E:\New folder (3)\CV_Waste_Sorting_Colab\desktop\Assets\app.ico";
            }
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
                try
                {
                    IntPtr hIconBig = LoadImage(IntPtr.Zero, iconPath, 1 /*IMAGE_ICON*/, 32, 32, 0x00000010 /*LR_LOADFROMFILE*/);
                    IntPtr hIconSmall = LoadImage(IntPtr.Zero, iconPath, 1 /*IMAGE_ICON*/, 16, 16, 0x00000010 /*LR_LOADFROMFILE*/);
                    if (hIconBig != IntPtr.Zero) SendMessage(hWnd, 0x0080 /*WM_SETICON*/, (IntPtr)1 /*ICON_BIG*/, hIconBig);
                    if (hIconSmall != IntPtr.Zero) SendMessage(hWnd, 0x0080 /*WM_SETICON*/, (IntPtr)0 /*ICON_SMALL*/, hIconSmall);
                }
                catch { }
            }
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    private async Task ConnectBackendAsync()
    {
        ServiceStatus.Text = "جارٍ تشغيل خدمة Python المحلية…";
        var ready = await _localApi.EnsureRunningAsync(_api);
        if (ready)
        {
            ServiceStatus.Text = "خدمة Python متصلة · localhost:8765";
            ServiceStatusDot.Fill = (SolidColorBrush)Application.Current.Resources["WasteEmeraldBrush"];
        }
        else
        {
            ServiceStatus.Text = "لم تبدأ خدمة Python · اضغط للتفاصيل";
            ServiceStatusDot.Fill = (SolidColorBrush)Application.Current.Resources["WasteRoseBrush"];
        }
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            _nav.NavigateTo(tag);
        }
    }

    private void UpdateNavSelection(string pageTag)
    {
        var target = Navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(item => item.Tag?.ToString() == pageTag);
        if (target is not null && Navigation.SelectedItem != target)
        {
            Navigation.SelectedItem = target;
        }
    }

    private void BtnQuickAction_Click(object sender, RoutedEventArgs e)
    {
        _nav.NavigateTo("data");
    }

    private void ThemeToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = root.ActualTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var text = (args.QueryText ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text)) return;

        if (text.Contains("بيان") || text.Contains("صور") || text.Contains("data") || text.Contains("image"))
            _nav.NavigateTo("data");
        else if (text.Contains("إعداد") || text.Contains("معامل") || text.Contains("setting") || text.Contains("config") || text.Contains("cnn"))
            _nav.NavigateTo("settings");
        else if (text.Contains("تدريب") || text.Contains("train") || text.Contains("عصر") || text.Contains("loss"))
            _nav.NavigateTo("training");
        else if (text.Contains("تقييم") || text.Contains("نتيجة") || text.Contains("نتائج") || text.Contains("result") || text.Contains("metric") || text.Contains("matrix") || text.Contains("chart") || text.Contains("مخطط"))
            _nav.NavigateTo("results");
        else if (text.Contains("كاميرا") || text.Contains("camera") || text.Contains("فيديو") || text.Contains("video") || text.Contains("مباشر") || text.Contains("live"))
            _nav.NavigateTo("camera");
        else if (text.Contains("اختبار") || text.Contains("فحص") || text.Contains("تجربة") || text.Contains("predict") || text.Contains("test"))
            _nav.NavigateTo("predict");
        else if (text.Contains("عن") || text.Contains("توثيق") || text.Contains("about") || text.Contains("info"))
            _nav.NavigateTo("about");
        else
            _nav.NavigateTo("overview");
    }
}
