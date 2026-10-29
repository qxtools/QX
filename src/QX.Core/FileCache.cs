using System.Text;

namespace Qx;

public sealed class FileCache
{
    readonly string _root;

    public FileCache(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    public async Task<string> GetTextAsync(string key, Func<CancellationToken, Task<string>> download,
        TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(download);
        if (lifetime is { } duration && duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        string path = Resolve(key);
        string? cached = await ReadAsync(path, lifetime, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
            return cached;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using FileStream lease = await FileLock.AcquireAsync(path + ".lock", cancellationToken).ConfigureAwait(false);
        cached = await ReadAsync(path, lifetime, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
            return cached;
        string content = await download(cancellationToken).ConfigureAwait(false);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
        return content;
    }

    string Resolve(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (Path.IsPathRooted(key) || key.Split('/', '\\').Any(part => part is ".." or "."))
            throw new ArgumentException("Cache keys must stay inside the cache directory.", nameof(key));
        string path = Path.GetFullPath(Path.Combine(_root, key));
        string relative = Path.GetRelativePath(_root, path);
        if (relative == "." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ArgumentException("Cache keys must stay inside the cache directory.", nameof(key));
        return path;
    }

    static async Task<string?> ReadAsync(string path, TimeSpan? lifetime, CancellationToken cancellation_token)
    {
        if (lifetime is { } duration && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) >= duration)
            return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellation_token).ConfigureAwait(false);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

}
