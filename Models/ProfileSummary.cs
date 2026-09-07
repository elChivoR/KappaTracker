namespace KappaTracker.Models
{
    public class ProfileSummary
    {
        public string SessionId { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public int Level { get; set; }
        public bool IsActive { get; set; }
    }
}
