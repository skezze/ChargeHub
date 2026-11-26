using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace ChargeHub.Updates;

internal static class InstallerUpdate
{
    const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{6E0C4B2A-9F3D-4C7E-8A1B-5D2F9E7C3A10}_is1";
    static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public static bool IsInstalledCopy()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var entry = machine.OpenSubKey(UninstallKey);
        return entry?.GetValue("InstallLocation") is string folder &&
            string.Equals(Path.TrimEndingDirectorySeparator(folder),
                Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase);
    }

    public static async Task DownloadAndLaunchAsync(InstallerAsset asset, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var token = timeout.Token;
        using var response = await Client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var path = Path.Combine(Path.GetTempPath(), $"ChargeHub-update-{Guid.NewGuid():N}.exe");
        bool launched = false;
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += count;
                    if (total > asset.Size) throw new InvalidDataException("Installer exceeds the release asset size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                }
                if (total != asset.Size) throw new InvalidDataException("Incomplete installer download.");
            }

            // Keep the verified file open without write/delete sharing until setup starts.
            await using var verified = File.OpenRead(path);
            var digest = await SHA256.HashDataAsync(verified, token);
            if (!digest.AsSpan().SequenceEqual(Convert.FromHexString(asset.Sha256)))
                throw new InvalidDataException("Installer SHA-256 does not match the release asset.");

            token.ThrowIfCancellationRequested();
            var signal = Guid.NewGuid().ToString("N");
            using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, $@"Local\ChargeHub.Update.{signal}");
            using var setup = Process.Start(new ProcessStartInfo(path,
                $"/SILENT /SUPPRESSMSGBOXES /NORESTART /ChargeHubUpdate={signal}") { UseShellExecute = true });
            if (setup is null) throw new InvalidOperationException("Could not start the installer.");

            // The Inno loader can start before its elevated setup has been approved.
            while (!ready.WaitOne(0))
            {
                if (setup.HasExited && !ready.WaitOne(0))
                    throw new InvalidOperationException("Setup closed before it was ready.");
                await Task.Delay(100, token);
            }
            launched = true;
        }
        finally
        {
            if (!launched)
            {
                try { File.Delete(path); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log.Error("Could not remove the update download", e); }
            }
        }
    }
}
