using System.Text.Json;

namespace WinOptimizer.Models;

public class RegistryBackup
{
    public string Hive { get; set; } = "";

    public string Path { get; set; } = "";

    public string Name { get; set; } = "";

    public bool Existed { get; set; }

    public string Type { get; set; } = "";

    public JsonElement? Value { get; set; }
}