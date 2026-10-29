namespace Qx;

public static class FileLock
{
    public static async Task<FileStream> AcquireAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                try
                {
                    return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException error) when ((error.HResult & 0xffff) is 11 or 32 or 33 or 35)
                {
                    await Task.Delay(50, timeout.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The shared cache is still locked by another process.");
        }
    }
}
