namespace KappaTracker.Models
{
    /// <summary>Where a Kappa quest sits for the active profile.</summary>
    public enum KappaQuestStatus
    {
        /// <summary>Not accepted yet / prerequisites unmet.</summary>
        Locked,

        /// <summary>Unlocked and can be picked up from the trader.</summary>
        Available,

        /// <summary>Accepted, objectives still pending.</summary>
        InProgress,

        /// <summary>All objectives done - just needs handing in.</summary>
        ReadyToHandIn,

        /// <summary>Finished.</summary>
        Completed,

        /// <summary>Failed / expired.</summary>
        Failed
    }
}
