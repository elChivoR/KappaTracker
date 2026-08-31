using System.Collections.Generic;

namespace KappaTracker.Models
{
    /// <summary>Snapshot of the profile the tracker is currently reporting on.</summary>
    public class PlayerProgress
    {
        public string Nickname { get; set; } = string.Empty;
        public int Level { get; set; } = 1;

        public HashSet<string> CompletedQuestIds { get; set; } = new();

        /// <summary>questId -&gt; where that quest sits for this profile.</summary>
        public Dictionary<string, KappaQuestStatus> QuestStatusById { get; set; } = new();

        public Dictionary<string, int> TraderLoyalty { get; set; } = new();

        /// <summary>questId -&gt; set of condition ids already satisfied for that quest.</summary>
        public Dictionary<string, HashSet<string>> CompletedConditionsByQuest { get; set; } = new();

        /// <summary>item tpl -&gt; total quantity held anywhere in the profile.</summary>
        public Dictionary<string, int> InventoryCounts { get; set; } = new();

        /// <summary>item tpl -&gt; quantity held that is flagged found-in-raid.</summary>
        public Dictionary<string, int> InventoryFirCounts { get; set; } = new();
    }
}
