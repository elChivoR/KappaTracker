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
    //   status : public CustomTextMeshProUGUI _status; field on QuestListItem — right-side
    //            status indicator ("active!", "ready to hand over", etc.).  The badge is
    //            prepended here when visible so it inherits the status color (orange for
    //            "active!").  Falls back to a colored <color=…>KAPPA · </color> prefix on
    //            _title when the status GameObject is inactive (quest not yet accepted).
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

        // Kappa badge color used as title fallback when the status label is hidden.
        // Matches EFT's "active!" gold so the badge reads as a status indicator.
        private const string KappaColor = "#00D4C8";

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

                var statusLabel = Traverse.Create(__instance).Field("_status").GetValue();
                if (statusLabel == null)
                    return;

                var go = Traverse.Create(statusLabel).Property("gameObject").GetValue();
                if (go == null)
                    return;

                bool statusVisible = Traverse.Create(go).Property("activeSelf").GetValue<bool>();
                var statusText = Traverse.Create(statusLabel).Property("text");

                if (statusVisible)
                {
                    // Badge in gold; status text keeps its existing color (e.g. orange for "active!").
                    var current = statusText.GetValue<string>();
                    if (!string.IsNullOrEmpty(current) && !current.StartsWith("KAPPA", StringComparison.Ordinal))
                        statusText.SetValue($"<color={KappaColor}>KAPPA · </color>" + current);
                }
                else
                {
                    // Status hidden (AvailableForStart / Locked): force-show with just the badge.
                    Traverse.Create(go).Method("SetActive", true).GetValue();
                    var current = statusText.GetValue<string>();
                    if (string.IsNullOrEmpty(current) || !current.StartsWith("KAPPA", StringComparison.Ordinal))
                        statusText.SetValue($"<color={KappaColor}>KAPPA</color>");
                }
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
