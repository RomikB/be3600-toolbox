namespace BE3600Toolbox.Core.Models;

public record MtdPartition(
    string Device,
    long Size,
    long EraseSize,
    string Name);

