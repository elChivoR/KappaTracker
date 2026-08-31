namespace KappaTracker.Models
{
    /// <summary>One quest in the backward path that unlocks another quest.</summary>
    public class PrereqNodeViewModel
    {
        public string QuestId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public KappaQuestStatus Status { get; set; } = KappaQuestStatus.Locked;

        /// <summary>This prerequisite is itself one of the 137 Kappa milestones.</summary>
        public bool IsKappaMilestone { get; set; }

        /// <summary>Short non-quest gate, e.g. "Lv 12" or "LL 2 Prapor". Null when none.</summary>
        public string? GateNote { get; set; }

        public bool IsDone => Status == KappaQuestStatus.Completed;
    }
}
