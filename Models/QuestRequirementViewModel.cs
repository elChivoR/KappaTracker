namespace KappaTracker.Models
{
    public class QuestRequirementViewModel
    {
        /// <summary>Raw condition type, e.g. "HandoverItem", "CounterCreator", "Level".</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>Human-readable line pulled from the game locale.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>True once the active profile has satisfied this condition.</summary>
        public bool IsMet { get; set; }

        // ----- item hand-over / find conditions only -----

        public bool IsItem { get; set; }
        public string? ItemName { get; set; }

        /// <summary>Template ids of the item(s) this condition asks for (icon lookup).</summary>
        public List<string> ItemIds { get; set; } = new();

        public int RequiredCount { get; set; }
        public int OwnedCount { get; set; }
        public bool FoundInRaidRequired { get; set; }

        /// <summary>Item requirement covered by what's currently in the stash/inventory.</summary>
        public bool HasEnoughItems => IsItem && OwnedCount >= RequiredCount;
    }
}
