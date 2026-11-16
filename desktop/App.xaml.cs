using System.IO;
using Microsoft.UI.Xaml;

namespace WasteVision.Desktop;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "startup_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UnhandledException: {e.Message}\n{e.Exception}\n");
            }
            catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "startup_log.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] OnLaunched Started.\n");
            _window = new MainWindow();
            _window.Activate();
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] MainWindow Activated successfully.\n");
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "startup_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Launch Exception: {ex.Message}\n{ex}\n");
            }
            catch { }
            throw;
        }
    }
}
