using System.Net.Http.Headers;
using System.Text.Json;

namespace ChargeHub.Updates;

internal enum CheckOutcome { UpToDate, Available, Failed }

internal sealed record CheckResult(CheckOutcome Outcome, ReleaseInfo? Release = null, string? Error = null);

/// <summary>
/// Asks the GitHub API for the latest published release (drafts and pre-releases are never
/// returned by that endpoint) and compares it with the running version. It only ever reads
/// public data and sends nothing but a User-Agent.
/// </summary>
internal sealed class UpdateChecker(HttpClient http, string repository)
{
    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public async Task<CheckResult> CheckAsync(Version current, CancellationToken cancellation)
    {
        try
        {
            using var response = await http.GetAsync($"https://api.github.com/repos/{repository}/releases/latest", cancellation).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new CheckResult(CheckOutcome.UpToDate); // no release published yet
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
            var release = Parse(json, repository);
            if (release is null) return new CheckResult(CheckOutcome.Failed, Error: "Unexpected response from GitHub");
            return IsNewer(release.Version, current)
                ? new CheckResult(CheckOutcome.Available, release)
                : new CheckResult(CheckOutcome.UpToDate);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            if (cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
            return new CheckResult(CheckOutcome.Failed, Error: e.Message);
        }
    }

    /// <summary>Reads <c>tag_name</c> and <c>html_url</c> from a release; null if they are missing or the tag is not a version.</summary>
    public static ReleaseInfo? Parse(string json, string repository = AppInfo.Repository)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty("tag_name", out var tagElement) || tagElement.GetString() is not { } tag) return null;
        if (!root.TryGetProperty("html_url", out var urlElement) || urlElement.GetString() is not { } url) return null;
        if (!url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) return null; // only ever open GitHub pages
        return TryParseVersion(tag, out var version)
            ? new ReleaseInfo(version, tag, url, ReadInstaller(root, repository, tag, version)) : null;
    }

    static InstallerAsset? ReadInstaller(JsonElement release, string repository, string tag, Version version)
    {
        if (version.Build < 0 || tag != $"v{version.ToString(3)}" ||
            !release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;

        var expectedUrl = $"https://github.com/{repository}/releases/download/{tag}/ChargeHub-{version.ToString(3)}-setup.exe";
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object ||
                !asset.TryGetProperty("browser_download_url", out var url) || url.ValueKind != JsonValueKind.String ||
                url.GetString() != expectedUrl ||
                !asset.TryGetProperty("digest", out var digest) || digest.ValueKind != JsonValueKind.String ||
                digest.GetString() is not { Length: 71 } hash || !hash.StartsWith("sha256:", StringComparison.Ordinal) ||
                !hash[7..].All(Uri.IsHexDigit) ||
                !asset.TryGetProperty("size", out var size) || size.ValueKind != JsonValueKind.Number ||
                !size.TryGetInt64(out long bytes) || bytes is <= 0 or > 100 * 1024 * 1024) continue;
            return new InstallerAsset(new Uri(expectedUrl), hash[7..], bytes);
        }
        return null;
    }

    /// <summary>Accepts "v1.2.3", "1.2" and "1.2.3-beta" (suffix ignored).</summary>
    public static bool TryParseVersion(string text, out Version version)
    {
        var trimmed = text.Trim().TrimStart('v', 'V');
        int cut = trimmed.IndexOfAny(['-', '+']);
        if (cut >= 0) trimmed = trimmed[..cut];
        if (Version.TryParse(trimmed, out var parsed) && parsed.Major >= 0)
        {
            version = parsed;
            return true;
        }
        version = new Version();
        return false;
    }

    /// <summary>Compares on major.minor.patch so "0.3" and "0.3.0" are the same version.</summary>
    public static bool IsNewer(Version candidate, Version current) =>
        Normalize(candidate) > Normalize(current);

    static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
