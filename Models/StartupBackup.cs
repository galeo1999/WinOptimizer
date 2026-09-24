namespace WinOptimizer.Models;

public class StartupBackup
{
    public string Hive { get; set; } = "";

    public string Path { get; set; } = "";

    public string Name { get; set; } = "";

    public bool Existed { get; set; }

    public string? BinaryValueBase64 { get; set; }
}