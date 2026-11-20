namespace ChargeHub;

/// <summary>
/// An invisible top-level window. It gives the app a thread-marshalling target
/// (<see cref="Control.BeginInvoke(Delegate)"/>) and receives system-wide broadcasts that
/// message-only windows do not get: hardware changes and resume from sleep.
/// </summary>
internal sealed class MessageWindow : Form
{
    const int WmPowerBroadcast = 0x0218, WmDeviceChange = 0x0219;
    const int PbtApmResumeSuspend = 0x7, PbtApmResumeAutomatic = 0x12;

    public MessageWindow()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        _ = Handle; // create the native window now; it is never shown
    }

    public event Action? HardwareChanged;

    public event Action? ResumedFromSleep;

    /// <summary>Runs <paramref name="action"/> on the UI thread; silently does nothing once the window is gone.</summary>
    public void Post(Action action)
    {
        try
        {
            if (!IsDisposed && IsHandleCreated) BeginInvoke(action);
        }
        catch (InvalidOperationException) { /* window handle destroyed while shutting down */ }
    }

    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WmDeviceChange) HardwareChanged?.Invoke();
        else if (m.Msg == WmPowerBroadcast && m.WParam.ToInt64() is PbtApmResumeSuspend or PbtApmResumeAutomatic) ResumedFromSleep?.Invoke();
    }
}
