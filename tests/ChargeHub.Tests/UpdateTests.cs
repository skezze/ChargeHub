using System.Net;
using ChargeHub.Settings;
using ChargeHub.Updates;
using Xunit;

namespace ChargeHub.Tests;

public class UpdateTests
{
    sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }

    const string Release = """{ "tag_name": "v0.4.0", "html_url": "https://github.com/o/r/releases/tag/v0.4.0", "draft": false }""";

    static UpdateChecker Checker(HttpMessageHandler handler) => new(new HttpClient(handler), "o/r");

    [Theory]
    [InlineData("v0.4.0", "0.3.0", true)]
    [InlineData("0.3.1", "0.3.0", true)]
    [InlineData("1.0", "0.9.9", true)]
    [InlineData("v0.3.0", "0.3.0", false)]
    [InlineData("0.3", "0.3.0", false)]
    [InlineData("0.2.9", "0.3.0", false)]
    public void Versions_are_compared_numerically(string candidate, string current, bool newer)
    {
        Assert.True(UpdateChecker.TryParseVersion(candidate, out var a));
        Assert.True(UpdateChecker.TryParseVersion(current, out var b));
        Assert.Equal(newer, UpdateChecker.IsNewer(a, b));
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("")]
    public void Non_version_tags_are_rejected(string tag) => Assert.False(UpdateChecker.TryParseVersion(tag, out _));

    [Fact]
    public void Prerelease_suffix_is_ignored()
    {
        Assert.True(UpdateChecker.TryParseVersion("v1.2.3-beta.1", out var version));
        Assert.Equal("1.2.3", version.ToString());
    }

    [Fact]
    public void Release_page_must_be_on_github()
    {
        Assert.NotNull(UpdateChecker.Parse(Release));
        Assert.Null(UpdateChecker.Parse("""{ "tag_name": "v0.4.0", "html_url": "https://evil.example/x" }"""));
        Assert.Null(UpdateChecker.Parse("""{ "tag_name": "v0.4.0" }"""));
        Assert.Null(UpdateChecker.Parse("[]"));
    }

    [Theory]
    [InlineData("o/r", "sha256", 123, true)]
    [InlineData("other/r", "sha256", 123, false)]
    [InlineData("o/r", "sha512", 123, false)]
    [InlineData("o/r", "sha256", 0, false)]
    public void Installer_requires_the_exact_release_url_and_valid_metadata(string repository, string algorithm, int size, bool accepted)
    {
        var json = $$"""
            { "tag_name": "v0.4.0", "html_url": "https://github.com/o/r/releases/tag/v0.4.0",
              "assets": [{ "browser_download_url": "https://github.com/{{repository}}/releases/download/v0.4.0/ChargeHub-0.4.0-setup.exe",
                "digest": "{{algorithm}}:{{new string('a', 64)}}", "size": {{size}} }] }
            """;
        var release = UpdateChecker.Parse(json, "o/r");
        Assert.NotNull(release);
        Assert.Equal(accepted, release.Installer is not null);
        if (accepted) Assert.Equal(size, release.Installer!.Size);
    }

    [Fact]
    public async Task Newer_release_is_reported()
    {
        var result = await Checker(new StubHandler(HttpStatusCode.OK, Release)).CheckAsync(new Version(0, 3, 0), CancellationToken.None);

        Assert.Equal(CheckOutcome.Available, result.Outcome);
        Assert.Equal("v0.4.0", result.Release!.Tag);
    }

    [Fact]
    public async Task Same_release_is_up_to_date()
    {
        var result = await Checker(new StubHandler(HttpStatusCode.OK, Release)).CheckAsync(new Version(0, 4, 0), CancellationToken.None);

        Assert.Equal(CheckOutcome.UpToDate, result.Outcome);
    }

    [Fact]
    public async Task Repository_without_releases_is_up_to_date()
    {
        var result = await Checker(new StubHandler(HttpStatusCode.NotFound, "{}")).CheckAsync(new Version(0, 3, 0), CancellationToken.None);

        Assert.Equal(CheckOutcome.UpToDate, result.Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Server_errors_are_failures(HttpStatusCode status)
    {
        var result = await Checker(new StubHandler(status, "{}")).CheckAsync(new Version(0, 3, 0), CancellationToken.None);

        Assert.Equal(CheckOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task Offline_and_garbage_are_failures_not_exceptions()
    {
        Assert.Equal(CheckOutcome.Failed, (await Checker(new ThrowingHandler()).CheckAsync(new Version(0, 3, 0), CancellationToken.None)).Outcome);
        Assert.Equal(CheckOutcome.Failed, (await Checker(new StubHandler(HttpStatusCode.OK, "<html>")).CheckAsync(new Version(0, 3, 0), CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task Service_checks_once_a_day_and_notifies_once_per_release()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var handler = new StubHandler(HttpStatusCode.OK, Release);
        var settings = new AppSettings();
        int saves = 0, notifications = 0;
        using var service = new UpdateService(settings, Checker(handler), new Version(0, 3, 0), () => saves++, () => now);
        service.NewReleaseFound += _ => notifications++;

        Assert.True(service.IsDue);
        await service.CheckIfDueAsync();

        Assert.Equal(UpdateStatus.Available, service.Status);
        Assert.Equal(1, notifications);
        Assert.Equal(now, settings.LastUpdateCheck);
        Assert.True(saves > 0);

        now += TimeSpan.FromHours(23);
        await service.CheckIfDueAsync();
        Assert.Equal(1, handler.Calls); // not due yet

        now += TimeSpan.FromHours(2);
        await service.CheckIfDueAsync();
        Assert.Equal(2, handler.Calls);
        Assert.Equal(1, notifications); // same release: no second notification
    }

    [Fact]
    public async Task Disabled_service_does_not_check_automatically()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Release);
        using var service = new UpdateService(new AppSettings { CheckForUpdates = false }, Checker(handler), new Version(0, 3, 0), () => { });

        await service.CheckIfDueAsync();

        Assert.Equal(0, handler.Calls);
        Assert.Equal(UpdateStatus.Idle, service.Status);
    }

    [Fact]
    public async Task Failed_check_keeps_a_known_update_and_is_retried()
    {
        var now = DateTimeOffset.UtcNow;
        var settings = new AppSettings();
        using var service = new UpdateService(settings, Checker(new StubHandler(HttpStatusCode.OK, Release)), new Version(0, 3, 0), () => { }, () => now);
        await service.CheckNowAsync();

        using var offline = new UpdateService(settings, Checker(new ThrowingHandler()), new Version(0, 3, 0), () => { }, () => now + TimeSpan.FromDays(2));
        await offline.CheckNowAsync();

        Assert.Equal(UpdateStatus.Failed, offline.Status); // nothing known in this instance
        Assert.Equal(now, settings.LastUpdateCheck); // a failure does not count as a check
        Assert.NotNull(service.Available);
    }
}
