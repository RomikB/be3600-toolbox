namespace BE3600Toolbox.Core.Services;

public interface ISshManager : IAsyncDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(string host, int port, string username, string password, CancellationToken ct = default);
    Task<string> ExecuteCommandAsync(string command, CancellationToken ct = default);
    Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress = null, CancellationToken ct = default);
    Task DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress = null, CancellationToken ct = default);
    Task DisconnectAsync();
}

