using System.Text.Json.Nodes;

namespace WasteVision.Desktop.Services.Interfaces;

public interface IWasteApiClient : IDisposable
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<JsonObject> GetAsync(string path, CancellationToken cancellationToken = default);
    Task<JsonObject> PostAsync(string path, object payload, CancellationToken cancellationToken = default);
    Task<JsonObject> UploadAsync(string path, string filePath, CancellationToken cancellationToken = default);
}
