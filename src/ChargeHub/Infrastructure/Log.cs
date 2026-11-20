using System.Globalization;

namespace ChargeHub;

/// <summary>Minimal file logger: thread-safe, size-capped, never throws.</summary>
internal static class Log
{
    const long MaxBytes = 512 * 1024;
    static readonly object Gate = new();

    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChargeHub");

    public static string FilePath => Path.Combine(Directory, "chargehub.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? error = null) => Write("ERROR", message, error);

    static void Write(string level, string message, Exception? error)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes) file.MoveTo(FilePath + ".old", true);
                var line = $"{DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)} {level} {message}";
                if (error is not null) line += $" | {error.GetType().Name}: {error.Message}";
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
        catch { /* logging must never take the app down */ }
    }
}
