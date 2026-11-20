namespace ChargeHub.Updates;

/// <summary>A published GitHub release.</summary>
internal sealed record ReleaseInfo(Version Version, string Tag, string PageUrl);
