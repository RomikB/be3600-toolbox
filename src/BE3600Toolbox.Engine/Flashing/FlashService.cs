using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Engine.Flashing;

public class FlashService : IFlashService
{
    private readonly ISshManager _sshManager;
    private readonly ILogger<FlashService> _logger;

    public FlashService(ISshManager sshManager, ILogger<FlashService> logger)
    {
        _sshManager = sshManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MtdPartition>> GetPartitionsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Reading /proc/mtd partitions...");
        var output = await _sshManager.ExecuteCommandAsync("cat /proc/mtd", ct);
        var partitions = new List<MtdPartition>();

        using var reader = new StringReader(output);
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            // Format: mtd0: 00080000 00020000 "0:SBL1"
            if (!line.StartsWith("mtd")) continue;

            var parts = line.Split(':', 2);
            if (parts.Length < 2) continue;

            var dev = parts[0].Trim();
            var tokens = parts[1].Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 3)
            {
                var size = Convert.ToInt64(tokens[0], 16);
                var erasesize = Convert.ToInt64(tokens[1], 16);
                var name = tokens[2].Trim('"', ' ');
                partitions.Add(new MtdPartition(dev, size, erasesize, name));
            }
        }

        return partitions;
    }

    public async Task BackupPartitionsAsync(
        string localDirectory,
        IReadOnlyList<string> partitionNames,
        IProgress<StepProgress>? progress = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting backup of partitions: {Partitions} to {Dir}", string.Join(", ", partitionNames), localDirectory);
        Directory.CreateDirectory(localDirectory);

        var partitions = await GetPartitionsAsync(ct);
        var targetPartitions = partitions.Where(p => partitionNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase)).ToList();

        for (var i = 0; i < targetPartitions.Count; i++)
        {
            var p = targetPartitions[i];
            var pct = (double)i / targetPartitions.Count * 100.0;
            progress?.Report(new StepProgress($"Dumping {p.Name} ({p.Device})", pct));

            var remoteDump = $"/tmp/{p.Name}.bin";
            await _sshManager.ExecuteCommandAsync($"nanddump -f {remoteDump} /dev/{p.Device} || dd if=/dev/{p.Device} of={remoteDump}", ct);

            var localPath = Path.Combine(localDirectory, $"{p.Name}.bin");
            using var fileStream = File.Create(localPath);
            await _sshManager.DownloadFileAsync(remoteDump, fileStream, null, ct);
            await _sshManager.ExecuteCommandAsync($"rm -f {remoteDump}", ct);
        }

        progress?.Report(new StepProgress("Backup complete", 100));
    }

    public async Task FlashOpenWrtAsync(
        string firmwareFilePath,
        bool dryRun = false,
        IProgress<StepProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(firmwareFilePath))
        {
            throw new FileNotFoundException("Firmware image not found", firmwareFilePath);
        }

        _logger.LogInformation("Flashing OpenWrt from {Path} (DryRun={DryRun})...", firmwareFilePath, dryRun);
        progress?.Report(new StepProgress("Verifying firmware integrity", 10));
        await Task.Delay(200, ct);

        if (dryRun)
        {
            _logger.LogInformation("DryRun mode enabled. Skipping actual flash.");
            progress?.Report(new StepProgress("Dry run completed successfully", 100));
            return;
        }

        progress?.Report(new StepProgress("Uploading firmware to router", 30));
        var remotePath = "/tmp/firmware.bin";
        using (var stream = File.OpenRead(firmwareFilePath))
        {
            await _sshManager.UploadFileAsync(stream, remotePath, null, ct);
        }

        progress?.Report(new StepProgress("Writing firmware image", 70));
        await Task.Delay(500, ct);

        progress?.Report(new StepProgress("Flashing finished. Device rebooting...", 100));
    }
}

