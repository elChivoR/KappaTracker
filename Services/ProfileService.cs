using KappaTracker.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.Profile;

namespace KappaTracker.Services
{
    [Injectable(InjectionType.Singleton)]
    public class ProfileService
    {
        private readonly ISptLogger<ProfileService> _logger;
        private readonly ProfileHelper _profileHelper;
        private readonly ProfileActivityService _profileActivity;

        public ProfileService(
            ISptLogger<ProfileService> logger,
            ProfileHelper profileHelper,
            ProfileActivityService profileActivityService)
        {
            _logger = logger;
            _profileHelper = profileHelper;
            _profileActivity = profileActivityService;
        }

        /// <summary>
        /// Returns progress for the most recently active PMC profile, falling back to the
        /// first initialised profile when no session has been active in the last 60 minutes.
        /// </summary>
        public PlayerProgress GetActivePlayer()
        {
            var allProfiles = _profileHelper.GetProfiles();
            var recentIds = _profileActivity.GetActiveProfileIdsWithinMinutes(60);

            // Prefer the most recently active profile that has an initialised PMC.
            foreach (var id in recentIds)
            {
                if (!allProfiles.TryGetValue(id, out var recent)) continue;
                var recentPmc = recent?.CharacterData?.PmcData;
                if (recentPmc?.Info is null) continue;
                return BuildProgress(recentPmc);
            }

            // Fall back: first initialised PMC on the server (original behaviour).
            foreach (var (_, profile) in allProfiles)
            {
                var pmc = profile?.CharacterData?.PmcData;
                if (pmc?.Info is null) continue;
                return BuildProgress(pmc);
            }

            _logger.Warning("[KappaTracker] No initialised PMC profile found; reporting zero progress");
            return new PlayerProgress();
        }

        private static PlayerProgress BuildProgress(SPTarkov.Server.Core.Models.Eft.Common.PmcData pmc)
        {
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

            var conditionProgress = new Dictionary<string, int>();
            foreach (var (_, counter) in pmc.TaskConditionCounters ?? [])
            {
                if (counter?.Id is null || counter.Value is not { } value || value <= 0)
                    continue;
                conditionProgress[(string)counter.Id] = (int)value;
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
                ConditionProgressById = conditionProgress,
                TraderLoyalty = loyalty,
                InventoryCounts = counts,
                InventoryFirCounts = firCounts
            };
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
