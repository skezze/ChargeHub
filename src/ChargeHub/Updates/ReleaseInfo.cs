namespace ChargeHub.Updates;

/// <summary>A published GitHub release.</summary>
internal sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, InstallerAsset? Installer = null);

internal sealed record InstallerAsset(Uri Url, string Sha256, long Size);
