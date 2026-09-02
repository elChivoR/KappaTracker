using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Web;

namespace KappaTracker;

public record ModMetadata : IModMetadata, IModBlazorMetadata
{
    public string ModGuid { get; init; } = "com.elchivor.kappatracker";
    public string Name { get; init; } = "KappaTracker";
    public string Author { get; init; } = "elChivoR";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(ModInfo.Version);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.3");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/elChivoR/KappaTracker";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;

    public string? WWWRootUrl { get; init; }
    public string? HomePage { get; init; } = "/kappa";
    public string? HomePageDescription { get; init; } = "Track your progress towards the Kappa secure container.";
}