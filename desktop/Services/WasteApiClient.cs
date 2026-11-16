using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using WasteVision.Desktop.Services.Interfaces;

namespace WasteVision.Desktop.Services;

public sealed class WasteApiClient : IWasteApiClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://127.0.0.1:8765/") };

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }

    public async Task<JsonObject> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(path, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw ApiError(body, response.StatusCode);
        return JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
    }

    public async Task<JsonObject> PostAsync(string path, object payload, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(path, payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw ApiError(body, response.StatusCode);
        return JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
    }

    public async Task<JsonObject> UploadAsync(string path, string filePath, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = File.OpenRead(filePath);
        using var file = new StreamContent(stream);
        file.Headers.ContentType = new MediaTypeHeaderValue(MimeFor(filePath));
        form.Add(file, "file", Path.GetFileName(filePath));
        using var response = await _http.PostAsync(path, form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw ApiError(body, response.StatusCode);
        return JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
    }

    private static Exception ApiError(string body, System.Net.HttpStatusCode status)
    {
        try { return new InvalidOperationException(JsonNode.Parse(body)?["detail"]?.GetValue<string>() ?? $"تعذر تنفيذ الطلب ({(int)status})."); }
        catch { return new InvalidOperationException($"تعذر تنفيذ الطلب ({(int)status})."); }
    }

    private static string MimeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".zip" => "application/zip",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };

    public void Dispose() => _http.Dispose();
}
