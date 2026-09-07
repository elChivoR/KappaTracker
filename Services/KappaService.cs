using KappaTracker.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace KappaTracker.Services
{
    [Injectable]
    public class KappaService
    {
        private readonly ISptLogger<KappaService> _logger;
        private readonly TradersTable _tradersTable;
        private readonly QuestService _questService;
        private readonly ProfileService _profileService;

        public KappaService(
            ISptLogger<KappaService> logger,
            TradersTable tradersTable,
            QuestService questService,
            ProfileService profileService)
        {
            _logger = logger;
            _tradersTable = tradersTable;
            _questService = questService;
            _profileService = profileService;
        }

        public KappaProgressViewModel GetKappaProgress(string? profileId = null)
        {
            var player = _profileService.GetActivePlayer(profileId);
            var byTrader = new Dictionary<string, TraderProgressViewModel>();
            var totalRequired = 0;
            var totalCompleted = 0;

            foreach (var trader in _tradersTable.Values)
            {
                if (trader.Base is null)
                    continue;

                var progress = BuildTraderProgress(trader, player);
                if (progress.QuestTotal == 0)
                    continue; // trader has no Kappa quests

                byTrader[progress.TraderId] = progress;
                totalRequired += progress.QuestTotal;
                totalCompleted += progress.QuestCompleted;
            }

            var percentage = totalRequired > 0
                ? totalCompleted / (double)totalRequired * 100
                : 0;

            return new KappaProgressViewModel
            {
                ProfileName = player.Nickname,
                ProfileLevel = player.Level,
                OverallPercentage = Math.Round(percentage, 2),
                TotalMissionsRequired = totalRequired,
                TotalMissionsCompleted = totalCompleted,
                LastUpdated = DateTime.UtcNow,
                ByTrader = byTrader
            };
        }

        public TraderProgressViewModel GetTraderProgress(string traderId, string? profileId = null)
        {
            var trader = _tradersTable.GetTrader(traderId);
            if (trader?.Base is null)
            {
                _logger.Warning($"[KappaTracker] Trader not found: {traderId}");
                return new TraderProgressViewModel { TraderId = traderId };
            }

            return BuildTraderProgress(trader, _profileService.GetActivePlayer(profileId));
        }

        private TraderProgressViewModel BuildTraderProgress(Trader trader, PlayerProgress player)
        {
            var traderId = (string)trader.Base!.Id;
            var quests = _questService.GetQuestsByTrader(traderId, includeChain: false);
            var completed = quests.Count(q => q.IsCompleted);
            var total = quests.Count;
            var percentage = total > 0 ? completed / (double)total * 100 : 0;

            return new TraderProgressViewModel
            {
                TraderId = traderId,
                Name = string.IsNullOrWhiteSpace(trader.Base.Nickname)
                    ? trader.Base.Surname ?? string.Empty
                    : trader.Base.Nickname,
                CurrentLevel = player.TraderLoyalty.GetValueOrDefault(traderId, 1),
                RequiredLevel = trader.Base.LoyaltyLevels?.Count ?? 4,
                QuestCompleted = completed,
                QuestTotal = total,
                ProgressPercentage = Math.Round(percentage, 2)
            };
        }
    }
}
