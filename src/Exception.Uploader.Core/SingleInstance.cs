namespace Exception.Uploader.Core;

/// <summary>
/// One uploader per user. Holding this file open is the lock, so a crash releases it. A second copy
/// asks the first to show its window, then exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    public const string ShowRequestFile = "show.request";
    private const string LockFile = "instance.lock";

    private readonly FileStream _lock;

    private SingleInstance(FileStream fileLock)
    {
        _lock = fileLock;
    }

    /// <summary>Null when another copy is running</summary>
    public static SingleInstance? TryAcquire(string folder)
    {
        try
        {
            return new SingleInstance(new FileStream(Path.Combine(folder, LockFile), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void RequestShow(string folder) =>
        File.WriteAllText(Path.Combine(folder, ShowRequestFile), DateTime.UtcNow.ToString("O"));

    public void Dispose() => _lock.Dispose();
}
