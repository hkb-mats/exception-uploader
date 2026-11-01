using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exception.Uploader.Core;

public enum UploadStatus
{
    Uploaded,

    /// <summary>The portal refused this export: only a changed file can do better</summary>
    Rejected,

    /// <summary>The token is revoked or wrong: nothing can upload until it's replaced</summary>
    TokenRefused,

    /// <summary>The network, the portal being down, or the rate limit: worth trying again</summary>
    Retry
}

public sealed record UploadResult(UploadStatus Status, string Message, int? Level = null);

public interface IPortalClient
{
    Task<UploadResult> UploadAsync(string token, string data, CancellationToken cancellationToken);
}

public sealed class PortalClient : IPortalClient
{
    private const string UploadPath = "api/addon-uploads/journal";
    private const string TokenHeader = "X-Upload-Token";

    private readonly HttpClient _http;

    public PortalClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<UploadResult> UploadAsync(string token, string data, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, UploadPath)
        {
            Content = JsonContent.Create(new UploadRequest(data), UploaderJson.Default.UploadRequest)
        };
        request.Headers.TryAddWithoutValidation(TokenHeader, token);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (System.Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new UploadResult(UploadStatus.Retry, "Can't reach the portal. Trying again soon.");
        }

        using (response)
        {
            return await ReadResultAsync(response, cancellationToken);
        }
    }

    private static async Task<UploadResult> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await TryReadAsync(response, UploaderJson.Default.UploadResponse, cancellationToken);
            return new UploadResult(UploadStatus.Uploaded, "Uploaded", result?.Level);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                new UploadResult(UploadStatus.TokenRefused, "The portal refused the token. Create a new one on your Profile page."),
            HttpStatusCode.TooManyRequests =>
                new UploadResult(UploadStatus.Retry, "Too many uploads. Trying again soon."),
            >= HttpStatusCode.InternalServerError =>
                new UploadResult(UploadStatus.Retry, "The portal is having trouble. Trying again soon."),
            _ => new UploadResult(UploadStatus.Rejected, await ReadProblemAsync(response, cancellationToken))
        };
    }

    private static async Task<string> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var problem = await TryReadAsync(response, UploaderJson.Default.ProblemResponse, cancellationToken);
        return problem?.Detail ?? problem?.Title ?? $"The portal refused the upload ({(int)response.StatusCode}).";
    }

    private static async Task<T?> TryReadAsync<T>(
        HttpResponseMessage response,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(type, cancellationToken);
        }
        catch (System.Exception e) when (e is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}

public sealed record UploadRequest(string Data);
public sealed record UploadResponse(int? Level);
public sealed record ProblemResponse(string? Title, string? Detail);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(UploadRequest))]
[JsonSerializable(typeof(UploadResponse))]
[JsonSerializable(typeof(ProblemResponse))]
[JsonSerializable(typeof(UploaderSettings))]
[JsonSerializable(typeof(Dictionary<string, CharacterRecord>))]
[JsonSerializable(typeof(UpdateInfo))]
[JsonSerializable(typeof(CharacterRecord))]
internal partial class UploaderJson : JsonSerializerContext;
