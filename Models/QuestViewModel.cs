using System.Collections.Generic;

namespace KappaTracker.Models
{
    public class QuestViewModel
    {
        public string QuestId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? GateNote { get; set; }

        /// <summary>Resolved map name for this quest (e.g. "Customs"); empty when not map-specific.</summary>
        public string Map { get; set; } = string.Empty;

        public KappaQuestStatus Status { get; set; } = KappaQuestStatus.Locked;
        public List<QuestRequirementViewModel> Requirements { get; set; } = new();

        public List<PrereqNodeViewModel> PrerequisiteChain { get; set; } = new();

        public int PrereqTotal => PrerequisiteChain.Count;
        public int PrereqCompletedCount => PrerequisiteChain.Count(n => n.GateSatisfied);

        /// <summary>
        /// Requirements that are done, or already covered by items on hand. This is the
        /// "how many of these can I satisfy right now" tally in the panel header, so it
        /// counts stash readiness even for quests that haven't been started yet.
        /// </summary>
        public int ReadyRequirementCount => Requirements.Count(r => r.IsMet || r.IsEnough);

        public bool IsCompleted => Status == KappaQuestStatus.Completed;
        public bool IsActive => Status is KappaQuestStatus.InProgress or KappaQuestStatus.ReadyToHandIn;
    }
}
