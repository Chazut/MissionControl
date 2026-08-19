using SPTarkov.Server.Core.Models.Spt.Mod;
using SemVerVersion = SemanticVersioning.Version;
using SemVerRange = SemanticVersioning.Range;

namespace MissionControl.Mod;

public sealed record MissionControlMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.chazut.missioncontrol";
    public string Name { get; init; } = "MissionControl";
    public string Author { get; init; } = "Chazut";
    public List<string>? Contributors { get; init; }
    public SemVerVersion Version { get; init; } = new("1.1.0");
    public SemVerRange SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemVerRange>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/Chazut/MissionControl";
    public string License { get; init; } = "MIT";
}
