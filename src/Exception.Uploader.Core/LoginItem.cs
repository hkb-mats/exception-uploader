using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace Exception.Uploader.Core;

/// <summary>Starts the uploader when the member logs in to their computer. Runs in the tray, with no window.</summary>
public static class LoginItem
{
    public const string BackgroundArgument = "--background";

    private const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string WindowsValue = "Exception Uploader";
    private const string MacLabel = "com.exception-guild.uploader";

    /// <summary>
    /// A Mac app opened from Downloads or its disk image runs from a path that's gone after a restart,
    /// so it can only start at login from Applications
    /// </summary>
    public static bool IsInApplications(string executablePath, string home) =>
        executablePath.StartsWith("/Applications/", StringComparison.Ordinal)
        || executablePath.StartsWith($"{home}/Applications/", StringComparison.Ordinal);

    public static void Set(bool enabled, string executablePath)
    {
        if (OperatingSystem.IsWindows())
            SetWindows(enabled, executablePath);
        else if (OperatingSystem.IsMacOS())
            SetMac(enabled, executablePath, MacAgentPath());
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindows(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(WindowsRunKey);
        if (enabled)
            key.SetValue(WindowsValue, $"\"{executablePath}\" {BackgroundArgument}");
        else
            key.DeleteValue(WindowsValue, throwOnMissingValue: false);
    }

    private static string MacAgentPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", $"{MacLabel}.plist");

    public static void SetMac(bool enabled, string executablePath, string agentPath)
    {
        if (!enabled)
        {
            File.Delete(agentPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(agentPath)!);
        File.WriteAllText(agentPath, MacAgent(executablePath));
    }

    public static string MacAgent(string executablePath) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
          <key>Label</key>
          <string>{MacLabel}</string>
          <key>ProgramArguments</key>
          <array>
            <string>{SecurityElement.Escape(executablePath)}</string>
            <string>{BackgroundArgument}</string>
          </array>
          <key>RunAtLoad</key>
          <true/>
          <key>LimitLoadToSessionType</key>
          <string>Aqua</string>
        </dict>
        </plist>
        """;
}
