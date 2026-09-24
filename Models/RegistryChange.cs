using System.Text.Json;

namespace WinOptimizer.Models;

public class RegistryChange
{
    public string Hive { get; set; } = "";

    public string Path { get; set; } = "";

    public string Name { get; set; } = "";

    public JsonElement Value { get; set; }

    public string Type { get; set; } = "DWord";
}