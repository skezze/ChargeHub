using Microsoft.Win32;

namespace ChargeHub.Settings;

/// <summary>Per-user "start with Windows" registration (HKCU Run key, no admin rights needed).</summary>
internal static class StartupRegistration
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "ChargeHub";

    /// <summary>The autostart command line; <c>--background</c> keeps the window closed so only the tray icon appears.</summary>
    public static string Command(string? exePath) => $"\"{exePath}\" {Program.BackgroundFlag}";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                Log.Error("Could not read startup registration", e);
                return false;
            }
        }
    }

    /// <summary>Entries written by older versions lack <c>--background</c> and would pop the window open at sign-in.</summary>
    public static void UpgradeLegacyCommand()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is string command && !command.Contains(Program.BackgroundFlag, StringComparison.Ordinal))
                key.SetValue(ValueName, $"{command.Trim()} {Program.BackgroundFlag}");
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Error("Could not upgrade the autostart entry", e);
        }
    }

    /// <returns><c>false</c> and an error message if the registry could not be changed.</returns>
    public static bool TrySet(bool enabled, out string? error)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, Command(Environment.ProcessPath));
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
            error = null;
            return true;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Error("Could not change autostart", e);
            error = e.Message;
            return false;
        }
    }
}
