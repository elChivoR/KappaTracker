using System;
using HarmonyLib;

namespace KappaTracker.Client
{
    // Spike notes (ILSpy on client/refs/Assembly-CSharp.dll):
    //   type   : EFT.UI.NotesTask  (public, : UIElement) — one row of the main Tasks screen list
    //   method : public void Show(Quest quest, IEftSession session, InventoryController inventoryController,
    //                             QuestController questController, NotesTaskDescriptionShort description,
    //                             FavoriteQuestManager favoriteQuests, bool availability)
    //            (single overload; inside it: `_taskLabel.text = quest.Template.Name;`)
    //   quest  : Show's `quest` parameter — EFT.Quests.Quest; quest.Template.Id is
    //            `[JsonProperty("_id")] public string Id { get; set; }` on EFT.Quests.QuestTemplate
    //            (the 24-hex template id; same value the milestone fetch stores).
    //            Also mirrored on the private `_quest` field, but the parameter avoids a stale
    //            pooled-row value on Show's early-return path.
    //   label  : public TextMeshProUGUI _taskLabel  (field on NotesTask) — mutated via its
    //            string `text` property through Traverse, so this assembly needs no
    //            UnityEngine.UI / TMPro reference.
    //   style  : B — mutate _taskLabel.text in a postfix (the name is assigned inside Show,
    //            not returned by a dedicated string method, so there is no Style-A target).
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

                var label = Traverse.Create(__instance).Field("_taskLabel").GetValue();
                if (label == null)
                    return;

                var textProp = Traverse.Create(label).Property("text");
                var current = textProp.GetValue<string>();
                textProp.SetValue(KappaTagService.Decorate(templateId, current));
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
