using ChargeHub.Settings;

namespace ChargeHub.Updates;

internal enum UpdateStatus { Idle, Checking, UpToDate, Available, Failed }

/// <summary>
/// Looks for a new release once a day. It never installs anything: when a newer release exists
/// it only remembers it, so the UI can offer to open the download page.
/// </summary>
internal sealed class UpdateService : IDisposable
{
    public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(24);
    static readonly TimeSpan WakeEvery = TimeSpan.FromHours(1); // also catches up after the PC was asleep

    readonly AppSettings settings;
    readonly UpdateChecker checker;
    readonly Version current;
    readonly Action persist;
    readonly Func<DateTimeOffset> clock;
    readonly CancellationTokenSource lifetime = new();
    readonly SemaphoreSlim running = new(1, 1);
    System.Threading.Timer? timer;

    public UpdateService(AppSettings settings, UpdateChecker checker, Version current, Action persist, Func<DateTimeOffset>? clock = null)
    {
        this.settings = settings;
        this.checker = checker;
        this.current = current;
        this.persist = persist;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public UpdateStatus Status { get; private set; } = UpdateStatus.Idle;

    /// <summary>The newer release, when <see cref="Status"/> is <see cref="UpdateStatus.Available"/>.</summary>
    public ReleaseInfo? Available { get; private set; }

    /// <summary>Raised on a background thread whenever <see cref="Status"/> changes.</summary>
    public event Action? Changed;

    /// <summary>Raised once per release version, the first time it is found.</summary>
    public event Action<ReleaseInfo>? NewReleaseFound;

    public bool IsDue => settings.CheckForUpdates
        && (settings.LastUpdateCheck is not { } last || clock() - last >= CheckEvery || last > clock() + TimeSpan.FromMinutes(5));

    public void Start()
    {
        timer ??= new System.Threading.Timer(_ => _ = CheckIfDueAsync(), null, TimeSpan.FromSeconds(30), WakeEvery);
    }

    public async Task CheckIfDueAsync()
    {
        if (IsDue) await CheckNowAsync().ConfigureAwait(false);
    }

    /// <summary>Checks right now (the "Check for updates" button). Concurrent calls are ignored.</summary>
    public async Task CheckNowAsync()
    {
        if (!await running.WaitAsync(0).ConfigureAwait(false)) return;
        try
        {
            SetStatus(UpdateStatus.Checking);
            var result = await checker.CheckAsync(current, lifetime.Token).ConfigureAwait(false);
            if (result.Outcome != CheckOutcome.Failed)
            {
                settings.LastUpdateCheck = clock();
                Available = result.Outcome == CheckOutcome.Available ? result.Release : null;
                persist();
            }
            else Log.Info($"Update check failed: {result.Error}"); // keep whatever was known before

            SetStatus(result.Outcome switch
            {
                CheckOutcome.Available => UpdateStatus.Available,
                CheckOutcome.UpToDate => UpdateStatus.UpToDate,
                _ => Available is null ? UpdateStatus.Failed : UpdateStatus.Available,
            });

            if (Available is { } release && settings.NotifiedUpdateTag != release.Tag)
            {
                settings.NotifiedUpdateTag = release.Tag;
                persist();
                NewReleaseFound?.Invoke(release);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        finally
        {
            running.Release();
        }
    }

    void SetStatus(UpdateStatus status)
    {
        Status = status;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        lifetime.Cancel();
        timer?.Dispose();
    }
}
