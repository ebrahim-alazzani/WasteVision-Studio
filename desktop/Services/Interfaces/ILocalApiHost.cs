namespace WasteVision.Desktop.Services.Interfaces;

public interface ILocalApiHost : IDisposable
{
    Task<bool> EnsureRunningAsync(IWasteApiClient api, CancellationToken cancellationToken = default);
}
