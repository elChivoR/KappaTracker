using KappaTracker.Services;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace KappaTracker;

/// <summary>
/// Entry point for the Kappa Tracker mod. Runs after SPT has loaded all game tables
/// (PostLoad + 1) so trader and quest data is available.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad + 1)]
public class KappaTrackerPlugin(ISptLogger<KappaTrackerPlugin> logger, KappaService kappaService) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var progress = kappaService.GetKappaProgress();
        logger.Success(
            $"[KappaTracker] Loaded. Kappa progress: {progress.OverallPercentage}% " +
            $"({progress.TotalMissionsCompleted}/{progress.TotalMissionsRequired} missions)");
        return Task.CompletedTask;
    }
}
