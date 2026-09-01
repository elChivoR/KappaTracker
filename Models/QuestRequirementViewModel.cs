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

        // ----- objective progress (any counted condition: hand-over, kills, ...) -----

        /// <summary>Goal count for this objective (condition value): 5 packs, 10 kills, ...</summary>
        public int TargetCount { get; set; }

        /// <summary>
        /// Live progress toward <see cref="TargetCount"/> read from the profile's task
        /// counters (packs handed over so far, Scavs killed so far, ...).
        /// </summary>
        public int CurrentCount { get; set; }

        // ----- item hand-over / find conditions only -----

        public bool IsItem { get; set; }
        public string? ItemName { get; set; }

        /// <summary>Template ids of the item(s) this condition asks for (icon lookup).</summary>
        public List<string> ItemIds { get; set; } = new();

        public int RequiredCount { get; set; }
        public int OwnedCount { get; set; }
        public bool FoundInRaidRequired { get; set; }

        // ----- display helpers -----

        /// <summary>Denominator to show: the objective goal.</summary>
        public int DisplayTarget => TargetCount > 0 ? TargetCount : RequiredCount;

        /// <summary>
        /// Numerator to show. Prefers real objective progress; for item conditions with
        /// no partial-progress counter it falls back to how many are held right now, so
        /// the "you have enough, go turn it in" hint still works.
        /// </summary>
        public int DisplayCurrent =>
            IsMet ? DisplayTarget
            : CurrentCount > 0 ? CurrentCount
            : IsItem ? OwnedCount
            : 0;

        /// <summary>This row has a meaningful X / Y counter worth rendering.</summary>
        public bool ShowCounter => IsItem || (Type == "CounterCreator" && DisplayTarget > 1);

        /// <summary>Objective is done, or progress plus what's held now covers the goal.</summary>
        public bool IsEnough => IsMet || (DisplayTarget > 0 && CurrentCount + OwnedCount >= DisplayTarget);
    }
}
