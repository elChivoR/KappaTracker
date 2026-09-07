using KappaTracker.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace KappaTracker.Services
{
    [Injectable]
    public class TraderService
    {
        // Fixed display order - TradersTable is a dictionary and its iteration order
        // is not stable across server restarts / mod load order.
        private static readonly string[] TraderOrder =
        {
            "54cb50c76803fa8b248b4571", // Prapor
            "54cb57776803fa99248b456e", // Therapist
            "579dc571d53a0658a154fbec", // Fence
            "58330581ace78e27b8b10cee", // Skier
            "5935c25fb3acc3127c3d8cd9", // Peacekeeper
            "5a7c2eca46aef81a7ca2145d", // Mechanic
            "5ac3b934156ae10c4430e83c", // Ragman
            "5c0647fdd443bc2504c2d371", // Jaeger
            "638f541a29ffd1183d187f57", // Lightkeeper
            "656f0f98d80a697f855d34b1", // BTR
            "6617beeaa9cfa777ca915b7c"  // Ref
        };

        private readonly ISptLogger<TraderService> _logger;
        private readonly TradersTable _tradersTable;
        private readonly QuestService _questService;
        private readonly ProfileService _profileService;

        public TraderService(
            ISptLogger<TraderService> logger,
            TradersTable tradersTable,
            QuestService questService,
            ProfileService profileService)
        {
            _logger = logger;
            _tradersTable = tradersTable;
            _questService = questService;
            _profileService = profileService;
        }

        /// <summary>Only traders that have at least one Kappa quest are returned.</summary>
        public List<TraderViewModel> GetAllTraders(string? profileId = null)
        {
            var player = _profileService.GetActivePlayer(profileId);
            var traders = new List<TraderViewModel>();

            foreach (var trader in _tradersTable.Values)
            {
                var baseData = trader.Base;
                if (baseData is null)
                    continue;

                var traderId = (string)baseData.Id;
                var quests = _questService.GetQuestsByTrader(traderId, includeChain: false, profileId: profileId);
                if (quests.Count == 0)
                    continue;

                var completed = quests.Count(q => q.IsCompleted);
                var percentage = quests.Count > 0 ? completed / (double)quests.Count * 100 : 0;

                traders.Add(new TraderViewModel
                {
                    TraderId = traderId,
                    Name = string.IsNullOrWhiteSpace(baseData.Nickname)
                        ? baseData.Surname ?? string.Empty
                        : baseData.Nickname,
                    CurrentLevel = player.TraderLoyalty.GetValueOrDefault(traderId, 1),
                    RequiredLevel = baseData.LoyaltyLevels?.Count ?? 4,
                    QuestCompleted = completed,
                    QuestTotal = quests.Count,
                    ProgressPercentage = Math.Round(percentage, 2)
                });
            }

            return traders
                .OrderBy(t => TraderRank(t.TraderId))
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int TraderRank(string traderId)
        {
            var index = Array.IndexOf(TraderOrder, traderId);
            return index >= 0 ? index : int.MaxValue;
        }
    }
}
