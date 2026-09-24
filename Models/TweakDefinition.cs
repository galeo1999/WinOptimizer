using System.Collections.Generic;

namespace WinOptimizer.Models;

public class TweakDefinition
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string Category { get; set; } = "";

    public string Risk { get; set; } = "Low";

    public bool RequiresAdmin { get; set; }

    public bool RequiresRestart { get; set; }

    public List<RegistryChange> RegistryChanges { get; set; } = [];
}