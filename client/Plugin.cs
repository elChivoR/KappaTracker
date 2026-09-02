using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace KappaTracker.Client
{
    [BepInPlugin("com.elchivor.kappatracker.client", "KappaTracker.Client", ModInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        private void Awake()
        {
            Log = Logger;
            KappaConfig.Init(Config);

            var harmony = new Harmony("com.elchivor.kappatracker.client");

            try { TasksScreenPatch.Patch(harmony); }
            catch (Exception ex) { Log.LogError($"KappaTracker: TasksScreen patch registration failed: {ex}"); }

            try { TraderTaskListPatch.Patch(harmony); }
            catch (Exception ex) { Log.LogError($"KappaTracker: TraderTaskList patch registration failed: {ex}"); }

            try { QuestDetailHeaderPatch.Patch(harmony); }
            catch (Exception ex) { Log.LogError($"KappaTracker: QuestDetailHeader patch registration failed: {ex}"); }

            try { InRaidTrackerPatch.Patch(harmony); }
            catch (Exception ex) { Log.LogError($"KappaTracker: InRaidTracker patch registration failed: {ex}"); }

            // One-shot: poll for the session token, then fetch the Kappa milestone id set once.
            StartCoroutine(MilestoneBootstrap.Run());

            Log.LogInfo($"KappaTracker.Client {ModInfo.Version} loaded.");
        }
    }
}
