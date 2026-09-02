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
            TasksScreenPatch.Patch(harmony);
            TraderTaskListPatch.Patch(harmony);
            QuestDetailHeaderPatch.Patch(harmony);
            InRaidTrackerPatch.Patch(harmony);

            // One-shot: poll for the session token, then fetch the Kappa milestone id set once.
            StartCoroutine(BootstrapPatch.Run());

            Log.LogInfo($"KappaTracker.Client {ModInfo.Version} loaded.");
        }
    }
}
