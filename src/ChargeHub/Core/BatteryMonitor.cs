namespace ChargeHub.Core;

/// <summary>
/// Polls all sources on a timer and on demand, merges the results and publishes a
/// new device list only when something actually changed. One loop, one poll at a
/// time; bursts of change requests are collapsed into a single poll.
/// </summary>
internal sealed class BatteryMonitor : IDisposable
{
    readonly IReadOnlyList<IBatterySource> sources;
    TimeSpan interval;
    readonly TimeSpan debounce;
    readonly TimeSpan sourceTimeout;
    readonly DeviceBook book = new();
    readonly Dictionary<IBatterySource, IReadOnlyList<DeviceReading>> latest = [];
    readonly SemaphoreSlim wake = new(0, 1);
    readonly CancellationTokenSource lifetime = new();
    Task? loop;

    public BatteryMonitor(IEnumerable<IBatterySource> sources, TimeSpan? interval = null, TimeSpan? debounce = null, TimeSpan? sourceTimeout = null)
    {
        this.sources = sources.ToArray();
        this.interval = interval ?? TimeSpan.FromMinutes(1);
        this.debounce = debounce ?? TimeSpan.FromMilliseconds(1500);
        this.sourceTimeout = sourceTimeout ?? TimeSpan.FromSeconds(20);
    }

    public IReadOnlyList<Device> Current { get; private set; } = [];

    /// <summary>Time between automatic polls. Takes effect after the current wait; call <see cref="RequestRefresh"/> to apply at once.</summary>
    public TimeSpan Interval
    {
        get => interval;
        set => interval = value < TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : value;
    }

    /// <summary>When the last poll finished, whether or not anything changed.</summary>
    public DateTimeOffset? LastPollAt { get; private set; }

    /// <summary>Raised on a background thread after every poll, even if nothing changed.</summary>
    public event Action? Polled;

    /// <summary>Raised on a background thread with the new device list.</summary>
    public event Action<IReadOnlyList<Device>>? Updated;

    public void Start()
    {
        if (loop is not null) return;
        foreach (var source in sources) source.Changed += OnSourceChanged;
        loop = Task.Run(() => RunAsync(lifetime.Token));
    }

    /// <summary>Asks for a poll soon. Safe to call from any thread, any number of times.</summary>
    public void RequestRefresh(TimeSpan? delay = null)
    {
        if (lifetime.IsCancellationRequested) return;
        if (delay is { } wait && wait > TimeSpan.Zero)
        {
            _ = Task.Delay(wait, lifetime.Token).ContinueWith(_ => RequestRefresh(), TaskContinuationOptions.OnlyOnRanToCompletion);
            return;
        }
        try { wake.Release(); }
        catch (SemaphoreFullException) { /* a refresh is already pending */ }
        catch (ObjectDisposedException) { }
    }

    void OnSourceChanged() => RequestRefresh();

    async Task RunAsync(CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await PollAsync(cancellation).ConfigureAwait(false);
                if (await wake.WaitAsync(interval, cancellation).ConfigureAwait(false))
                {
                    // Let a burst of hardware events settle, then swallow the ones that arrived meanwhile.
                    await Task.Delay(debounce, cancellation).ConfigureAwait(false);
                    wake.Wait(0, CancellationToken.None);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Log.Error("Monitor loop crashed", e); }
    }

    /// <summary>Runs one poll of every source. Exposed for the loop and for one-shot diagnostics.</summary>
    public async Task PollAsync(CancellationToken cancellation)
    {
        var results = await Task.WhenAll(sources.Select(source => ReadSourceAsync(source, cancellation))).ConfigureAwait(false);
        foreach (var (source, readings) in results) latest[source] = readings;

        var devices = book.Update(results.SelectMany(r => r.Readings), DateTimeOffset.Now);
        LastPollAt = DateTimeOffset.Now;
        bool changed = !devices.SequenceEqual(Current);
        if (changed)
        {
            Current = devices;
            Updated?.Invoke(devices);
        }
        Polled?.Invoke();
    }

    async Task<(IBatterySource Source, IReadOnlyList<DeviceReading> Readings)> ReadSourceAsync(IBatterySource source, CancellationToken cancellation)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(sourceTimeout);
            return (source, await source.ReadAsync(timeout.Token).ConfigureAwait(false));
        }
        catch (Exception e) when (!cancellation.IsCancellationRequested)
        {
            Log.Error($"{source.Name} source failed", e);
            // Keep what we knew about the devices, but stop claiming anything about their state.
            var degraded = latest.TryGetValue(source, out var previous)
                ? previous.Select(r => DeviceReading.Unavailable(r.Id, r.Name, r.Transport)).ToArray()
                : [];
            return (source, degraded);
        }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        foreach (var source in sources) source.Changed -= OnSourceChanged;
        try { loop?.Wait(TimeSpan.FromSeconds(1)); } catch (AggregateException) { }
        foreach (var source in sources) (source as IDisposable)?.Dispose();
    }
}
