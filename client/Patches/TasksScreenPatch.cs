using System;
using HarmonyLib;

namespace KappaTracker.Client
{
    // Spike notes (ILSpy on client/refs/Assembly-CSharp.dll):
    //   type   : EFT.UI.NotesTask  (public, : UIElement) — one row of the main Tasks screen list
    //   method : public void Show(Quest quest, IEftSession session, InventoryController inventoryController,
    //                             QuestController questController, NotesTaskDescriptionShort description,
    //                             FavoriteQuestManager favoriteQuests, bool availability)
    //            (single overload; `_statusLabel.text` is always set inside Show for Started /
    //            AvailableForFinish / MarkedAsFailed — the only statuses passed to NotesTask).
    //   quest  : Show's `quest` parameter — EFT.Quests.Quest; quest.Template.Id is
    //            `[JsonProperty("_id")] public string Id { get; set; }` on EFT.Quests.QuestTemplate
    //            (the 24-hex template id; same value the milestone fetch stores).
    //   label  : public TextMeshProUGUI _timerLabel (field on NotesTask) — normally hidden for
    //            non-daily quests, positioned in the column between the quest name and location.
    //            We force it active and set it to the KAPPA badge in cyan, leaving _statusLabel
    //            ("active!", etc.) completely untouched.
    internal static class TasksScreenPatch
    {
        public static void Patch(Harmony h)
        {
            var target = AccessTools.Method("EFT.UI.NotesTask:Show");
            if (target == null)
            {
                Plugin.Log.LogError("KappaTracker: TasksScreen hook not found; surface disabled.");
                return;
            }
            h.Patch(target, postfix: new HarmonyMethod(typeof(TasksScreenPatch), nameof(Postfix)));
        }

        private static bool _loggedError;

        private const string KappaColor = "#00D4C8";

        private static void Postfix(object __instance, object quest)
        {
            try
            {
                if (!KappaConfig.Enabled.Value || !KappaConfig.TasksScreen.Value || !KappaTagService.Ready)
                    return;
                if (__instance == null || quest == null)
                    return;

                var templateId = Traverse.Create(quest).Property("Template").Property("Id").GetValue<string>();
                if (string.IsNullOrEmpty(templateId) || !KappaTagService.IsKappa(templateId))
                    return;

                // Activate the timer label column (hidden for non-daily quests) and show the badge.
                // _statusLabel ("active!", etc.) is left completely untouched.
                var timerLabel = Traverse.Create(__instance).Field("_timerLabel").GetValue();
                if (timerLabel == null)
                    return;

                var go = Traverse.Create(timerLabel).Property("gameObject").GetValue();
                if (go == null)
                    return;

                Traverse.Create(go).Method("SetActive", true).GetValue();
                Traverse.Create(timerLabel).Property("text").SetValue($"<color={KappaColor}>KAPPA</color>");
            }
            catch (Exception ex)
            {
                if (_loggedError) return;
                _loggedError = true;
                Plugin.Log.LogError($"KappaTracker: TasksScreen patch failed once ({ex.GetType().Name}: {ex.Message}); suppressing further errors.");
            }
        }
    }
}
