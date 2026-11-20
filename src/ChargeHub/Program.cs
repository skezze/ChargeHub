namespace ChargeHub;

internal static class Program
{
    public const string BackgroundFlag = "--background";
    const string ProbeFlag = "--probe";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains(ProbeFlag, StringComparer.OrdinalIgnoreCase))
            return Probe.RunAsync().GetAwaiter().GetResult();

        // One instance only. A second launch asks the first one to open its window and exits.
        using var mutex = new Mutex(true, @"Local\ChargeHub.Tray", out bool isFirst);
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ChargeHub.Show");
        if (!isFirst)
        {
            showSignal.Set();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Unhandled UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);

        try
        {
            bool background = args.Contains(BackgroundFlag, StringComparer.OrdinalIgnoreCase);
            using var app = new TrayApp(showWindow: !background);
            var registration = ThreadPool.RegisterWaitForSingleObject(showSignal, (_, _) => app.RequestShowWindow(), null, Timeout.Infinite, executeOnlyOnce: false);
            try
            {
                Application.Run(app); // returns after ExitThread; disposing the app removes every tray icon
            }
            finally
            {
                registration.Unregister(null);
            }
        }
        catch (Exception e)
        {
            Log.Error("Fatal error", e);
            return 1;
        }
        return 0;
    }
}
