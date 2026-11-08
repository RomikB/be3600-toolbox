using System.Net.Sockets;
using BE3600Toolbox.Core.Services;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace BE3600Toolbox.Engine.Ssh;

public class SshManager : ISshManager
{
    private readonly ILogger<SshManager> _logger;
    private SshClient? _sshClient;
    private ScpClient? _scpClient;

    public SshManager(ILogger<SshManager> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _sshClient?.IsConnected == true;

    public async Task<bool> IsPortOpenAsync(string host, int port = 22, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(2));

            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public Task ConnectAsync(string host, int port, string username, string password, CancellationToken ct = default)
    {
        _logger.LogInformation("Connecting to SSH at {Host}:{Port} as {User}...", host, port, username);

        DisconnectInternal();

        var connectionInfo = new ConnectionInfo(host, port, username, new PasswordAuthenticationMethod(username, password))
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var sshClient = new SshClient(connectionInfo);
        var scpClient = new ScpClient(connectionInfo);

        try
        {
            sshClient.Connect();
            scpClient.Connect();

            _sshClient = sshClient;
            _scpClient = scpClient;

            _logger.LogInformation("SSH connected successfully to {Host}", host);
            return Task.CompletedTask;
        }
        catch
        {
            sshClient.Dispose();
            scpClient.Dispose();
            throw;
        }
    }

    public async Task<bool> TryConnectAsync(string host, int port, string username, string password, CancellationToken ct = default)
    {
        try
        {
            await ConnectAsync(host, port, username, password, ct).ConfigureAwait(false);
            return true;
        }
        catch (Renci.SshNet.Common.SshAuthenticationException)
        {
            _logger.LogWarning("SSH authentication failed for {User}@{Host}:{Port}", username, host, port);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to SSH at {Host}:{Port}", host, port);
            return false;
        }
    }

    public Task<string> ExecuteCommandAsync(string command, CancellationToken ct = default)
    {
        if (_sshClient == null || !_sshClient.IsConnected)
        {
            throw new InvalidOperationException("SSH client is not connected.");
        }

        _logger.LogDebug("Executing remote command: {Command}", command);
        using var cmd = _sshClient.CreateCommand(command);
        var result = cmd.Execute();

        if (cmd.ExitStatus != 0)
        {
            _logger.LogWarning("Command '{Command}' exited with code {ExitCode}. Error: {Error}", command, cmd.ExitStatus, cmd.Error);
        }

        return Task.FromResult(result);
    }

    public Task UploadFileAsync(Stream source, string remotePath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (_scpClient == null || !_scpClient.IsConnected)
        {
            throw new InvalidOperationException("SCP client is not connected.");
        }

        _logger.LogInformation("Uploading file to remote path: {RemotePath}...", remotePath);
        _scpClient.Uploading += (sender, e) =>
        {
            if (e.Size > 0)
            {
                progress?.Report((double)e.Uploaded / e.Size * 100.0);
            }
        };

        _scpClient.Upload(source, remotePath);
        return Task.CompletedTask;
    }

    public Task DownloadFileAsync(string remotePath, Stream destination, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (_scpClient == null || !_scpClient.IsConnected)
        {
            throw new InvalidOperationException("SCP client is not connected.");
        }

        _logger.LogInformation("Downloading remote file {RemotePath}...", remotePath);
        _scpClient.Downloading += (sender, e) =>
        {
            if (e.Size > 0)
            {
                progress?.Report((double)e.Downloaded / e.Size * 100.0);
            }
        };

        _scpClient.Download(remotePath, destination);
        return Task.CompletedTask;
    }

    private void DisconnectInternal()
    {
        _scpClient?.Disconnect();
        _scpClient?.Dispose();
        _scpClient = null;

        _sshClient?.Disconnect();
        _sshClient?.Dispose();
        _sshClient = null;
    }

    public Task DisconnectAsync()
    {
        DisconnectInternal();
        _logger.LogInformation("SSH disconnected.");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}

