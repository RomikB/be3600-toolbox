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

    public Task ConnectAsync(string host, int port, string username, string password, CancellationToken ct = default)
    {
        _logger.LogInformation("Connecting to SSH at {Host}:{Port} as {User}...", host, port, username);
        
        var connectionInfo = new ConnectionInfo(host, port, username, new PasswordAuthenticationMethod(username, password))
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        _sshClient = new SshClient(connectionInfo);
        _scpClient = new ScpClient(connectionInfo);

        _sshClient.Connect();
        _scpClient.Connect();

        _logger.LogInformation("SSH connected successfully to {Host}", host);
        return Task.CompletedTask;
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

    public Task DisconnectAsync()
    {
        _scpClient?.Disconnect();
        _scpClient?.Dispose();
        _scpClient = null;

        _sshClient?.Disconnect();
        _sshClient?.Dispose();
        _sshClient = null;

        _logger.LogInformation("SSH disconnected.");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        GC.SuppressFinalize(this);
    }
}

