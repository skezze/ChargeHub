using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using ChargeHub.Core;

namespace ChargeHub;

/// <summary>Plain-text summary for bug reports: environment plus what the app currently knows about each device.</summary>
internal static class DiagnosticReport
{
    /// <param name="includeIds">Device ids contain hardware addresses, so they are left out unless asked for.</param>
    public static string Build(IReadOnlyList<Device> devices, DateTimeOffset? polledAt, bool includeIds)
    {
        var text = new StringBuilder();
        text.AppendLine($"{AppInfo.Name} {AppInfo.Version}");
        text.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        text.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        text.AppendLine($"Last poll: {(polledAt is { } at ? at.ToString("s", CultureInfo.InvariantCulture) : "never")}");
        text.AppendLine($"Devices: {devices.Count}");
        foreach (var device in devices)
        {
            text.Append($"- {device.Name} | {device.Transport} | {device.Link} | ");
            text.Append(device.Percent is int percent ? $"{percent}%{(device.Charging ? " charging" : "")}" : "no reading");
            if (device.LastPercent is int last) text.Append($" | last {last}%");
            if (includeIds) text.Append($" | {device.Id}");
            text.AppendLine();
        }
        return text.ToString();
    }
}
