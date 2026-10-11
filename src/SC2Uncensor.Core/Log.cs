using System.Text;

namespace SC2Uncensor.Core;

public static class Log
{
    private static readonly object Gate = new();
    private static StreamWriter? _file;
    public static event Action<string>? LineWritten;

    public static void Open(string path)
    {
        lock (Gate)
        {
            _file?.Dispose();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            _file = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
        }
    }

    public static void Info(string message) => Write(message);

    public static void Write(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        lock (Gate)
        {
            _file?.WriteLine(line);
        }
        LineWritten?.Invoke(line);
    }

    public static void Banner(string message)
    {
        lock (Gate)
        {
            _file?.WriteLine(message);
        }
        LineWritten?.Invoke(message);
    }
}
