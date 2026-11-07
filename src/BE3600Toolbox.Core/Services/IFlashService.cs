using BE3600Toolbox.Core.Models;

namespace BE3600Toolbox.Core.Services;

public interface IFlashService
{
    Task<IReadOnlyList<MtdPartition>> GetPartitionsAsync(CancellationToken ct = default);
    Task BackupPartitionsAsync(string localDirectory, IReadOnlyList<string> partitionNames, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
    Task FlashOpenWrtAsync(string firmwareFilePath, bool dryRun = false, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
}

