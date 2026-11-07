namespace BE3600Toolbox.Core.Models;

public record RouterInfo(
    string IpAddress,
    string Model,
    string HardwareVersion,
    string FirmwareVersion,
    string? Channel = null,
    bool IsSshOpen = false);

