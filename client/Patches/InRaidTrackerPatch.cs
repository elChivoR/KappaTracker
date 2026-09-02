using HarmonyLib;

namespace KappaTracker.Client
{
    // Spike (ILSpy on client/refs/Assembly-CSharp.dll, ~45 min timebox) — OUTCOME (b): NO RELIABLE HOOK.
    //
    // Goal was the component that renders a task NAME in an on-screen in-raid objectives
    // HUD (a persistent list of active tasks + objectives shown during a raid). This game
    // build has no such component: EFT does not draw an always-on task list in raid.
    //
    // Commands run (all `& $ils Assembly-CSharp.dll ...`, output filtered):
    //   -l c | Select-String 'Quest' | Select-String 'Widget|Panel|Tracker|Hud|Notif|Objective'
    //   -l c | Select-String 'Widget'                         -> zero results (no *Widget type at all)
    //   -l c | Select-String 'Tracker|InRaidQuest|QuestPanel|TasksHud|QuestMarker'
    //                                                         -> only unrelated BadValueLogTracker / Audio.TransformPositionTracker
    //   -l c | grep 'Class EFT\.UI\.\S*(Raid|InGame|Hud|Tracker|Objective|Progress|Notif)\S*'
    //   -t "EFT.UI.GameUI"        (the in-raid HUD root) -> SerializeField panels only:
    //                             ExtractionTimersPanel, BattleUIPanel*, UsingPanel, EventStatePanel, ...
    //                             NO quest/task/objective panel field.
    //   -t "EFT.UI.EftBattleUIScreen"  -> raid HUD screen controller; wires no quest/task view.
    //
    // Candidates inspected and why each was rejected:
    //   EFT.UI.QuestProgressView   — `Show(Quest quest)` but sets only `_percentages.text`
    //                                (a "0..100" completion number + fill bar). No task name. Reject.
    //   EFT.UI.QuestObjectivesView — `Show(...)` per quest, hosts the objective rows and a fail
    //                                `_timer`, holds `_quest`, but renders NO task-name label
    //                                (only conditions + timer). Used inside QuestView, not the HUD. Reject.
    //   EFT.UI.QuestObjectiveView  — `Show(Quest, Condition, ...)`: one objective/condition row
    //                                (handover button, child conditions). Condition text, not the
    //                                task title. Reject.
    //   EFT.UI.QuestView           — trader quest-detail panel (Accept/re-roll buttons, Trader param).
    //                                `_title` is the known-dead field (see QuestDetailHeaderPatch notes);
    //                                the visible header is NotesTaskDescription, already Surface 3. Reject.
    //   EFT.UI.QuestsScreen / QuestsListView / QuestListItem — trader dialogue quest list, already
    //                                Surface 2 (TraderTaskListPatch). Not in-raid. Reject.
    //   EFT.UI.TasksScreen / NotesTask — the notes/tasks screen (default `O` in raid) reuses the SAME
    //                                `EFT.UI.NotesTask:Show` rows already hooked by Surface 1
    //                                (TasksScreenPatch), which fires in raid as well as the menu —
    //                                so in-raid task rows are ALREADY tagged there. No separate hook needed.
    //   EFT.Communications.NotificationQuest — in-raid quest updates surface only as free-text
    //                                `NotificationWithText` toasts ("Task ...: objective completed").
    //                                No structured task-name field; decorating would need brittle string
    //                                parsing of localized text. Reject.
    //
    // Conclusion: no dedicated in-raid objective-tracker component exists to hook, and the one
    // in-raid task view that does exist (the notes screen) is covered by Surface 1. Shipping this
    // surface DISABLED per the brief's outcome (b). KappaConfig.InRaidTracker default is `false`;
    // flip it to `true` to retry once (and if) BSG ships a real in-raid task HUD with a name label,
    // then replace Patch() below with a concrete AccessTools.Method(...) + Postfix.
    internal static class InRaidTrackerPatch
    {
        public static void Patch(Harmony h)
        {
            Plugin.Log.LogInfo(
                "KappaTracker: in-raid tracker hook not identified; surface disabled. " +
                "Set [Surfaces] InRaidTracker = true to retry once a hook ships.");
        }
    }
}
