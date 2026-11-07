namespace BE3600Toolbox.Core.Models;

public record DetectedRouter(
    string IpAddress,
    string Description,
    bool IsOpenWrt = false);

