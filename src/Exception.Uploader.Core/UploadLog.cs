namespace Exception.Uploader.Core;

public interface IUploadLog
{
    void Write(string line);
}

/// <summary>
/// A short file for support: what happened, per character. Callers pass only names and outcomes, never
/// the token or the export. Starts over past 1 MB.
/// </summary>
public sealed class FileUploadLog : IUploadLog
{
    private const long MaxBytes = 1_000_000;

    private readonly string _path;
    private readonly object _lock = new();

    public FileUploadLog(string path)
    {
        _path = path;
    }

    public void Write(string line)
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                    File.Delete(_path);
                File.AppendAllText(_path, $"{DateTime.UtcNow:O} {line}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Never let a log line stop an upload
            }
        }
    }
}
