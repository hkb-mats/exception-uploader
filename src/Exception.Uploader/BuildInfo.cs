using System.Reflection;

namespace Exception.Uploader;

/// <summary>Addresses baked in at build time; EXCEPTION_UPLOADER_API points a local build at a local API</summary>
internal static class BuildInfo
{
    private static string? LocalApi => Environment.GetEnvironmentVariable("EXCEPTION_UPLOADER_API");

    public static bool IsLocalApi => !string.IsNullOrEmpty(LocalApi);
    public static string ApiUrl => IsLocalApi ? LocalApi! : Metadata("UploaderApiUrl");

    public static string PortalUrl => Metadata("UploaderPortalUrl");
    public static string DownloadUrl => Metadata("UploaderDownloadUrl");

    public static Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    private static string Metadata(string key) =>
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value ?? string.Empty;
}
