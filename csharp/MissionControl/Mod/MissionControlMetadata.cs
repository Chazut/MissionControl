using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Web;
using SemVerVersion = SemanticVersioning.Version;
using SemVerRange = SemanticVersioning.Range;

namespace MissionControl.Mod;

public sealed record MissionControlMetadata : IModMetadata, IModBlazorMetadata
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
    public string? WWWRootUrl { get; init; }
    public string? HomePage { get; init; } = "/missioncontrol";
    public string? HomePageDescription { get; init; } = "Configure MissionControl";
}
