using System.Collections.Generic;

namespace KappaTracker.Models
{
    public class QuestViewModel
    {
        public string QuestId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? GateNote { get; set; }
        public KappaQuestStatus Status { get; set; } = KappaQuestStatus.Locked;
        public List<QuestRequirementViewModel> Requirements { get; set; } = new();

        public List<PrereqNodeViewModel> PrerequisiteChain { get; set; } = new();

        public int PrereqTotal => PrerequisiteChain.Count;
        public int PrereqCompletedCount => PrerequisiteChain.Count(n => n.GateSatisfied);

        public bool IsCompleted => Status == KappaQuestStatus.Completed;
        public bool IsActive => Status is KappaQuestStatus.InProgress or KappaQuestStatus.ReadyToHandIn;
    }
}
