using System;
using HarmonyLib;

namespace KappaTracker.Client
{
    // Spike notes (ILSpy on client/refs/Assembly-CSharp.dll):
    //   type   : EFT.UI.NotesTaskDescription  (public, : UIElement) — the quest detail
    //            panel (image + title bar + status + description). QuestView holds it as
    //            `public NotesTaskDescription _descriptionPanel;` and drives it from
    //            `QuestView.UpdateView()` via `_descriptionPanel.Show(_quest, _backendSession)`.
    //            QuestView._title is an unused leftover field — the visible header text is
    //            rendered here. Distinct from Task 5 (EFT.UI.NotesTask) and Task 6
    //            (EFT.UI.QuestListItem).
    //   method : public void Show(Quest quest, IImageLoader session) — sets
    //            `_title.text = quest.Template.Name;` (also _location/_description/_status).
    //            Harmony binds the `quest` parameter by name into the postfix.
    //   quest  : Show's `quest` parameter — EFT.Quests.Quest; quest.Template.Id is the
    //            24-hex template id, reached as
    //            Traverse.Create(quest).Property("Template").Property("Id").
    //   label  : public TMP_Text _title;  field on NotesTaskDescription — mutated via its
    //            string `text` property through Traverse, so this assembly needs no
    //            UnityEngine.UI / TMPro reference.
    internal static class QuestDetailHeaderPatch
    {
        public static void Patch(Harmony h)
        {
            var target = AccessTools.Method("EFT.UI.NotesTaskDescription:Show");
            if (target == null)
            {
                Plugin.Log.LogError("KappaTracker: QuestDetailHeader hook not found; surface disabled.");
                return;
            }
            h.Patch(target, postfix: new HarmonyMethod(typeof(QuestDetailHeaderPatch), nameof(Postfix)));
        }

        private static bool _loggedError;

        private static void Postfix(object __instance, object quest)
        {
            try
            {
                if (!KappaConfig.Enabled.Value || !KappaConfig.QuestDetailHeader.Value || !KappaTagService.Ready)
                    return;
                if (__instance == null || quest == null)
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
                Plugin.Log.LogError($"KappaTracker: QuestDetailHeader patch failed once ({ex.GetType().Name}: {ex.Message}); suppressing further errors.");
            }
        }
    }
}
