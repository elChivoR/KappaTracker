using BepInEx.Configuration;

namespace KappaTracker.Client
{
    internal static class KappaConfig
    {
        public static ConfigEntry<bool> Enabled = null!;
        public static ConfigEntry<bool> TasksScreen = null!;
        public static ConfigEntry<bool> TraderTaskList = null!;
        public static ConfigEntry<bool> QuestDetailHeader = null!;
        public static ConfigEntry<bool> InRaidTracker = null!;

        public static void Init(ConfigFile cfg)
        {
            Enabled = cfg.Bind("General", "Enabled", true,
                "Master switch for the KAPPA prefix on in-game task rows.");
            TasksScreen = cfg.Bind("Surfaces", "TasksScreen", true,
                "Tag rows in the main Tasks screen list.");
            TraderTaskList = cfg.Bind("Surfaces", "TraderTaskList", true,
                "Tag rows in the trader dialogue task list.");
            QuestDetailHeader = cfg.Bind("Surfaces", "QuestDetailHeader", true,
                "Tag the quest name in the task detail header.");
            InRaidTracker = cfg.Bind("Surfaces", "InRaidTracker", false,
                "Tag task names in the in-raid objective tracker (HUD). (currently disabled — no in-game hook available)");
        }
    }
}
