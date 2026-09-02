using System;
using HarmonyLib;

namespace KappaTracker.Client
{
    // Spike notes (ILSpy on client/refs/Assembly-CSharp.dll):
    //   type   : EFT.UI.QuestListItem  (public, : UIElement) — one row of the trader
    //            dialogue quest list. QuestsListView builds these via
    //            `new BindableViewList<Quest, QuestListItem>(bindableList, _questListItemPrefab, ...)`
    //            inside `QuestsListView.Show(IEftSession, InventoryController, QuestController, Trader, QuestView)`.
    //            This is NOT the same row type as Task 5 (EFT.UI.NotesTask); the two
    //            screens use different list items, so this surface needs its own patch.
    //   method : public void UpdateView()  (no parameters) — first line is
    //            `_title.text = Quest.Template.Name;` (re-runs on every status change,
    //            each time resetting _title.text to the plain name first, so the
    //            postfix re-decorates from clean text — no double prefix).
    //   quest  : public Quest Quest;  field on QuestListItem — Quest.Template.Id is the
    //            24-hex template id (same value the milestone fetch stores), reached as
    //            Traverse.Create(__instance).Field("Quest").Property("Template").Property("Id").
    //   label  : public CustomTextMeshProUGUI _title;  field on QuestListItem — mutated
    //            via its string `text` property through Traverse, so this assembly needs
    //            no UnityEngine.UI / TMPro reference.
    internal static class TraderTaskListPatch
    {
        public static void Patch(Harmony h)
        {
            var target = AccessTools.Method("EFT.UI.QuestListItem:UpdateView");
            if (target == null)
            {
                Plugin.Log.LogError("KappaTracker: TraderTaskList hook not found; surface disabled.");
                return;
            }
            h.Patch(target, postfix: new HarmonyMethod(typeof(TraderTaskListPatch), nameof(Postfix)));
        }

        private static bool _loggedError;

        private static void Postfix(object __instance)
        {
            try
            {
                if (!KappaConfig.Enabled.Value || !KappaConfig.TraderTaskList.Value || !KappaTagService.Ready)
                    return;
                if (__instance == null)
                    return;

                var quest = Traverse.Create(__instance).Field("Quest").GetValue();
                if (quest == null)
                    return;

                var templateId = Traverse.Create(quest).Property("Template").Property("Id").GetValue<string>();
                if (string.IsNullOrEmpty(templateId) || !KappaTagService.IsKappa(templateId))
                    return;

                var label = Traverse.Create(__instance).Field("_title").GetValue();
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
                Plugin.Log.LogError($"KappaTracker: TraderTaskList patch failed once ({ex.GetType().Name}: {ex.Message}); suppressing further errors.");
            }
        }
    }
}
