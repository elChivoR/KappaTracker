using System;
using System.Collections.Generic;

namespace KappaTracker.Models
{
    public class KappaProgressViewModel
    {
        public string ProfileName { get; set; } = string.Empty;
        public double OverallPercentage { get; set; }
        public int TotalMissionsRequired { get; set; }
        public int TotalMissionsCompleted { get; set; }
        public DateTime LastUpdated { get; set; }
        public Dictionary<string, TraderProgressViewModel> ByTrader { get; set; } = new();
    }

    public class TraderProgressViewModel
    {
        public string TraderId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int QuestCompleted { get; set; }
        public int QuestTotal { get; set; }
        public int CurrentLevel { get; set; }
        public int RequiredLevel { get; set; }
        public double ProgressPercentage { get; set; }
    }
}
