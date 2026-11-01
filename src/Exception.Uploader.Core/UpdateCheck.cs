using System.Net.Http.Json;
using System.Text.Json;

namespace Exception.Uploader.Core;

/// <summary>uploader.json, next to the builds: written by .github/workflows/uploader.yml</summary>
public sealed record UpdateInfo(string Version);

public static class UpdateCheck
{
    /// <summary>The newer version on offer, or null. Never fails: this is only a hint.</summary>
    public static async Task<string?> FindNewerAsync(HttpClient http, string infoUrl, Version current, CancellationToken cancellationToken)
    {
        try
        {
            var info = await http.GetFromJsonAsync(infoUrl, UploaderJson.Default.UpdateInfo, cancellationToken);
            return info is not null && Version.TryParse(info.Version, out var latest) && latest > current ? info.Version : null;
        }
        catch (System.Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException or NotSupportedException)
        {
            return null;
        }
    }
}
