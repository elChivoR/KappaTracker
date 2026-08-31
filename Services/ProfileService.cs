using KappaTracker.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Enums;

namespace KappaTracker.Services
{
    /// <summary>
    /// Reads the server's stored profiles to work out which quests / conditions are done,
    /// each trader's loyalty level, and what items the player is holding.
    /// </summary>
    [Injectable(InjectionType.Singleton)]
    public class ProfileService
    {
        private readonly ISptLogger<ProfileService> _logger;
        private readonly ProfileHelper _profileHelper;

        public ProfileService(
            ISptLogger<ProfileService> logger,
            ProfileHelper profileHelper)
        {
            _logger = logger;
            _profileHelper = profileHelper;
        }

        /// <summary>
        /// Progress for the first initialised PMC profile on the server, or an empty
        /// snapshot when no usable profile exists yet.
        /// </summary>
        public PlayerProgress GetActivePlayer()
        {
            foreach (var (_, profile) in _profileHelper.GetProfiles())
            {
                var pmc = profile?.CharacterData?.PmcData;
                if (pmc?.Info is null)
                    continue;

                var completedQuests = new HashSet<string>();
                var completedConditions = new Dictionary<string, HashSet<string>>();
                var statusById = new Dictionary<string, KappaQuestStatus>();
                foreach (var quest in pmc.Quests ?? [])
                {
                    var questId = (string)quest.QId;
                    statusById[questId] = MapStatus(quest.Status);
                    if (quest.Status == QuestStatusEnum.Success)
                        completedQuests.Add(questId);

                    if (quest.CompletedConditions is { Count: > 0 })
                        completedConditions[questId] = new HashSet<string>(quest.CompletedConditions);
                }

                var loyalty = new Dictionary<string, int>();
                foreach (var (traderId, info) in pmc.TradersInfo ?? [])
                    loyalty[traderId] = info.LoyaltyLevel ?? 1;

                var counts = new Dictionary<string, int>();
                var firCounts = new Dictionary<string, int>();
                foreach (var item in pmc.Inventory?.Items ?? [])
                {
                    var tpl = (string)item.Template;
                    var qty = (int)(item.Upd?.StackObjectsCount ?? 1);
                    counts[tpl] = counts.GetValueOrDefault(tpl) + qty;
                    if (item.Upd?.SpawnedInSession == true)
                        firCounts[tpl] = firCounts.GetValueOrDefault(tpl) + qty;
                }

                return new PlayerProgress
                {
                    Nickname = pmc.Info.Nickname ?? string.Empty,
                    Level = pmc.Info.Level ?? 1,
                    CompletedQuestIds = completedQuests,
                    QuestStatusById = statusById,
                    CompletedConditionsByQuest = completedConditions,
                    TraderLoyalty = loyalty,
                    InventoryCounts = counts,
                    InventoryFirCounts = firCounts
                };
            }

            _logger.Warning("[KappaTracker] No initialised PMC profile found; reporting zero progress");
            return new PlayerProgress();
        }

        private static KappaQuestStatus MapStatus(QuestStatusEnum status) => status switch
        {
            QuestStatusEnum.Success => KappaQuestStatus.Completed,
            QuestStatusEnum.Started => KappaQuestStatus.InProgress,
            QuestStatusEnum.AvailableForFinish => KappaQuestStatus.ReadyToHandIn,
            QuestStatusEnum.AvailableForStart => KappaQuestStatus.Available,
            QuestStatusEnum.AvailableAfter => KappaQuestStatus.Available,
            QuestStatusEnum.Fail => KappaQuestStatus.Failed,
            QuestStatusEnum.FailRestartable => KappaQuestStatus.Failed,
            QuestStatusEnum.MarkedAsFailed => KappaQuestStatus.Failed,
            QuestStatusEnum.Expired => KappaQuestStatus.Failed,
            _ => KappaQuestStatus.Locked
        };
    }
}
