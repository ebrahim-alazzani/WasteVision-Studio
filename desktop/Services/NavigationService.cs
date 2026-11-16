using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WasteVision.Desktop.Models;
using WasteVision.Desktop.Services.Interfaces;
using WasteVision.Desktop.Views;

namespace WasteVision.Desktop.Services;

public sealed class NavigationService : INavigationService
{
    private readonly Frame _frame;
    private readonly IWasteApiClient _api;
    private readonly Window _owner;
    private readonly Hyperparameters _params = new();
    private readonly Action<string> _onNavigate;
    private readonly Dictionary<string, Page> _cache = new();

    public NavigationService(Frame frame, IWasteApiClient api, Window owner, Action<string> onNavigate)
    {
        _frame = frame;
        _api = api;
        _owner = owner;
        _onNavigate = onNavigate;
    }

    public void NavigateTo(string pageTag)
    {
        if (!_cache.TryGetValue(pageTag, out var page))
        {
            page = pageTag switch
            {
                "data" => new DataPage(_api, _owner),
                "settings" => new SettingsPage(this, _params),
                "training" => new TrainingPage(_api, this, _params),
                "results" => new ResultsPage(_api, this),
                "camera" => new LiveCameraPage(_api, _owner),
                "predict" => new PredictionPage(_api, _owner),
                "about" => new AboutPage(),
                _ => new OverviewPage(_api, this)
            };
            _cache[pageTag] = page;
        }

        _frame.Content = page;
        if (page is ResultsPage resultsPage)
        {
            _ = resultsPage.RefreshAsync();
        }
        else if (page is TrainingPage trainingPage)
        {
            _ = trainingPage.RestoreCurrentStateAsync();
        }
        _onNavigate(pageTag);
    }
}
