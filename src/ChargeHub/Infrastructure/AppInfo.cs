using System.Reflection;

namespace ChargeHub;

internal static class AppInfo
{
    public const string Name = "ChargeHub";

    /// <summary>GitHub repository ("owner/name") that publishes releases; the update check reads it.</summary>
    public const string Repository = "skezze/ChargeHub";

    /// <summary>Product version without the "+commit" suffix the SDK appends.</summary>
    public static string Version { get; } = Read();

    public static System.Version CurrentVersion { get; } =
        Updates.UpdateChecker.TryParseVersion(Version, out var parsed) ? parsed : new System.Version(0, 0, 0);

    static string Read()
    {
        var info = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info)) return "0.0.0";
        int plus = info.IndexOf('+');
        return plus < 0 ? info : info[..plus];
    }
}
