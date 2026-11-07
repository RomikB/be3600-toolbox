namespace BE3600Toolbox.Core.Models;

public class AppSettings
{
    public string RouterIp { get; set; } = string.Empty;
    public int SshPort { get; set; } = 22;
    public string SshUser { get; set; } = "root";
    public string Language { get; set; } = "ru";
    public string? LastBackupPath { get; set; }
}
