namespace KappaTracker.Models
{
    public class TraderViewModel
    {
        public string TraderId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int CurrentLevel { get; set; }
        public int RequiredLevel { get; set; }
        public int QuestCompleted { get; set; }
        public int QuestTotal { get; set; }
        public double ProgressPercentage { get; set; }
    }
}
