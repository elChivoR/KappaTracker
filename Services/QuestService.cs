using KappaTracker.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils.Json;

namespace KappaTracker.Services
{
    [Injectable(InjectionType.Singleton)]
    public class QuestService
    {
        /// <summary>"Collector" - the quest that unlocks the Kappa secure container.</summary>
        private const string CollectorQuestId = "5c51aac186f77432ea65c552";

        /// <summary>Game default. The tracker ignores the server's own locale setting.</summary>
        public const string DefaultLanguage = "en";

        private readonly ISptLogger<QuestService> _logger;
        private readonly QuestHelper _questHelper;
        private readonly LocaleService _localeService;
        private readonly ProfileService _profileService;
        private readonly QuestGraphService _questGraph;

        private HashSet<string>? _kappaQuestIds;
        private bool _kappaQuestIdsAreFallback;
        private Dictionary<string, string>? _localeCache;
        private string _language = DefaultLanguage;

        /// <summary>
        /// True when <see cref="GetKappaQuestIds"/> could not derive the list from the
        /// Collector quest and fell back to returning every quest in the database. Callers
        /// that tag "Kappa quests" (e.g. the in-game plugin) should treat the set as
        /// unusable in that case. Only meaningful after <see cref="GetKappaQuestIds"/> has run.
        /// </summary>
        public bool KappaQuestIdsAreFallback
        {
            get
            {
                if (_kappaQuestIds is null)
                    GetKappaQuestIds();
                return _kappaQuestIdsAreFallback;
            }
        }

        /// <summary>
        /// Language used for quest names/descriptions. Defaults to English; set this
        /// (e.g. from a future UI selector) to switch. Invalidates the locale cache.
        /// </summary>
        public string Language
        {
            get => _language;
            set
            {
                var lang = string.IsNullOrWhiteSpace(value) ? DefaultLanguage : value.Trim().ToLowerInvariant();
                if (lang == _language)
                    return;
                _language = lang;
                _localeCache = null;
            }
        }

        public QuestService(
            ISptLogger<QuestService> logger,
            QuestHelper questHelper,
            LocaleService localeService,
            ProfileService profileService,
            QuestGraphService questGraph)
        {
            _logger = logger;
            _questHelper = questHelper;
            _localeService = localeService;
            _profileService = profileService;
            _questGraph = questGraph;
        }

        /// <summary>
        /// Quest IDs required for the Kappa container: the "Collector" quest plus every
        /// quest it lists as a completion prerequisite. Falls back to every quest in the
        /// database if the Collector quest can't be found (e.g. heavily modded setups).
        /// </summary>
        public HashSet<string> GetKappaQuestIds()
        {
            if (_kappaQuestIds is not null)
                return _kappaQuestIds;

            var allQuests = _questHelper.GetQuestsFromDb();
            var collector = allQuests.FirstOrDefault(q => (string)q.Id == CollectorQuestId);

            // Collector lists its prerequisite quests as "Quest" conditions under
            // AvailableForStart (AvailableForFinish holds the item hand-ins).
            var ids = new HashSet<string>();
            var conditions = collector?.Conditions;
            if (conditions is not null)
            {
                foreach (var cond in Concat(conditions.AvailableForStart, conditions.AvailableForFinish))
                {
                    if (cond.ConditionType != "Quest" && cond.Type != "Quest")
                        continue;

                    foreach (var target in ReadTargets(cond.Target))
                        ids.Add(target);
                }
            }

            if (ids.Count == 0)
            {
                _logger.Warning(
                    "[KappaTracker] Could not derive the Kappa quest list from the Collector quest; " +
                    "falling back to ALL quests");
                _kappaQuestIdsAreFallback = true;
                foreach (var quest in allQuests)
                    ids.Add(quest.Id);
            }
            else
            {
                _kappaQuestIdsAreFallback = false;
                ids.Add(CollectorQuestId);
            }

            _kappaQuestIds = ids;
            _logger.Success($"[KappaTracker] Tracking {ids.Count} Kappa quests");
            return _kappaQuestIds;
        }

        /// <summary>
        /// Kappa-relevant quests for a trader, with localised name/description and their
        /// completion state for the active profile.
        /// </summary>
        public List<QuestViewModel> GetQuestsByTrader(string traderId, bool includeChain = true, string? profileId = null)
        {
            var kappaIds = GetKappaQuestIds();
            var player = _profileService.GetActivePlayer(profileId);
            var quests = new List<QuestViewModel>();

            foreach (var quest in _questHelper.GetQuestsFromDb())
            {
                var questId = (string)quest.Id;
                if (!kappaIds.Contains(questId) || (string)quest.TraderId != traderId)
                    continue;

                var rawStatus = player.QuestStatusById.GetValueOrDefault(questId, KappaQuestStatus.Locked);
                var (status, viaAlt) = ResolveAlternativeRoute(questId, rawStatus, player);
                quests.Add(new QuestViewModel
                {
                    QuestId = questId,
                    Title = ResolveQuestName(questId, quest),
                    Description = ResolveLocale($"{questId} description"),
                    Map = ResolveMapName(quest.Location),
                    Status = status,
                    SatisfiedViaAlternativeTitle = viaAlt,
                    Requirements = BuildRequirements(quest, player, status),
                    PrerequisiteChain = includeChain ? BuildChain(questId, kappaIds, player) : new()
                });
            }

            // Active quests first, then available, locked, done, failed - then by name.
            return quests
                .OrderBy(q => SortRank(q.Status))
                .ThenBy(q => q.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Full detail for an arbitrary quest id (not trader-scoped): English name/description,
        /// live status for the active profile, hand-in requirements, and its start-gate note.
        /// Returns null if the id is not in the quest database. PrerequisiteChain is left empty.
        /// </summary>
        public QuestViewModel? GetQuestDetail(string questId, string? profileId = null)
        {
            var quest = _questHelper.GetQuestsFromDb().FirstOrDefault(q => (string)q.Id == questId);
            if (quest is null)
                return null;

            var player = _profileService.GetActivePlayer(profileId);
            var rawStatus = player.QuestStatusById.GetValueOrDefault(questId, KappaQuestStatus.Locked);
            var (status, viaAlt) = ResolveAlternativeRoute(questId, rawStatus, player);

            return new QuestViewModel
            {
                QuestId = questId,
                Title = ResolveQuestName(questId, quest),
                Description = ResolveLocale($"{questId} description"),
                Map = ResolveMapName(quest.Location),
                Status = status,
                SatisfiedViaAlternativeTitle = viaAlt,
                Requirements = BuildRequirements(quest, player, status),
                GateNote = _questGraph.GetGateNote(questId)
            };
        }

        /// <summary>
        /// A Kappa quest that reads as <see cref="KappaQuestStatus.Failed"/> only because
        /// the player committed to a mutually exclusive route still counts as done:
        /// resolve it to <see cref="KappaQuestStatus.Completed"/> and report the quest
        /// that satisfied it. Any other status is returned unchanged.
        /// </summary>
        private (KappaQuestStatus Status, string? ViaTitle) ResolveAlternativeRoute(
            string questId, KappaQuestStatus status, PlayerProgress player)
        {
            if (status != KappaQuestStatus.Failed)
                return (status, null);

            foreach (var altId in _questGraph.GetMutuallyExclusiveQuestIds(questId))
            {
                if (!player.CompletedQuestIds.Contains(altId))
                    continue;

                var name = ResolveLocale($"{altId} name");
                return (KappaQuestStatus.Completed, string.IsNullOrWhiteSpace(name) ? altId : name);
            }

            return (status, null);
        }

        private static int SortRank(KappaQuestStatus status) => status switch
        {
            KappaQuestStatus.ReadyToHandIn => 0,
            KappaQuestStatus.InProgress => 1,
            KappaQuestStatus.Available => 2,
            KappaQuestStatus.Locked => 3,
            KappaQuestStatus.Completed => 4,
            KappaQuestStatus.Failed => 5,
            _ => 6
        };

        /// <summary>
        /// Turns a quest's completion conditions into readable requirement rows. Item
        /// hand-over / find conditions are cross-checked against the profile inventory.
        /// </summary>
        private List<QuestRequirementViewModel> BuildRequirements(
            Quest quest, PlayerProgress player, KappaQuestStatus status)
        {
            var questId = (string)quest.Id;
            // ReadyToHandIn means every finish-condition is already satisfied.
            var questDone = status is KappaQuestStatus.Completed or KappaQuestStatus.ReadyToHandIn;
            player.CompletedConditionsByQuest.TryGetValue(questId, out var doneConditions);

            var rows = new List<QuestRequirementViewModel>();
            foreach (var cond in quest.Conditions?.AvailableForFinish ?? [])
            {
                var conditionId = (string)cond.Id;
                var type = !string.IsNullOrWhiteSpace(cond.ConditionType)
                    ? cond.ConditionType!
                    : cond.Type ?? string.Empty;

                var row = new QuestRequirementViewModel
                {
                    Type = type,
                    Description = ResolveLocale(conditionId),
                    IsMet = questDone || (doneConditions?.Contains(conditionId) ?? false)
                };

                // Objective progress for any counted condition (hand-over, kills, ...).
                row.TargetCount = (int)(cond.Value ?? 0);
                row.CurrentCount = row.IsMet
                    ? row.TargetCount
                    : player.ConditionProgressById.GetValueOrDefault(conditionId);
                if (row.TargetCount > 0 && row.CurrentCount > row.TargetCount)
                    row.CurrentCount = row.TargetCount;

                if (type is "HandoverItem" or "FindItem")
                {
                    // A condition can list many interchangeable tpls (e.g. every BEAR
                    // dogtag variant). Count ownership across all of them, but only show
                    // one representative icon.
                    var tpls = ReadTargets(cond.Target).ToList();
                    row.IsItem = true;
                    row.ItemIds = tpls.Take(1).ToList();
                    row.RequiredCount = (int)(cond.Value ?? 1);
                    row.FoundInRaidRequired = cond.OnlyFoundInRaid == true;
                    row.ItemName = ResolveItemName(tpls);
                    row.OwnedCount = tpls.Sum(tpl => row.FoundInRaidRequired
                        ? player.InventoryFirCounts.GetValueOrDefault(tpl)
                        : player.InventoryCounts.GetValueOrDefault(tpl));
                }

                if (string.IsNullOrWhiteSpace(row.Description))
                    row.Description = FallbackDescription(type);

                rows.Add(row);
            }

            return rows;
        }

        private List<PrereqNodeViewModel> BuildChain(
            string questId, HashSet<string> kappaIds, PlayerProgress player)
        {
            var rows = new List<PrereqNodeViewModel>();
            foreach (var edge in _questGraph.GetAncestorChain(questId))
            {
                var live = player.QuestStatusById.GetValueOrDefault(edge.QuestId, KappaQuestStatus.Locked);
                var title = ResolveLocale($"{edge.QuestId} name");
                if (string.IsNullOrWhiteSpace(title))
                    title = edge.QuestName ?? string.Empty;

                rows.Add(new PrereqNodeViewModel
                {
                    QuestId = edge.QuestId,
                    Title = title,
                    Status = live,
                    IsKappaMilestone = kappaIds.Contains(edge.QuestId),
                    GateNote = edge.GateNote,
                    GateSatisfied = live == KappaQuestStatus.Completed
                                    || edge.SatisfyingStatuses.Contains(live)
                });
            }
            return rows;
        }

        private string ResolveItemName(IReadOnlyList<string> tpls)
        {
            var names = tpls
                .Select(tpl =>
                {
                    var name = ResolveLocale($"{tpl} Name");
                    return string.IsNullOrWhiteSpace(name) ? ResolveLocale($"{tpl} ShortName") : name;
                })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
                return string.Empty;

            return names.Count <= 3
                ? string.Join(" / ", names)
                : $"{string.Join(" / ", names.Take(3))} +{names.Count - 3}";
        }

        private static string FallbackDescription(string type) => type switch
        {
            "HandoverItem" => "Hand over item",
            "FindItem" => "Find item",
            "CounterCreator" => "Complete objective",
            "Level" => "Reach required level",
            "TraderLoyalty" => "Reach trader loyalty level",
            "TraderStanding" => "Reach trader standing",
            "Skill" => "Reach required skill level",
            "Quest" => "Complete prerequisite quest",
            "PlaceBeacon" => "Place item at location",
            "LeaveItemAtLocation" => "Leave item at location",
            "" => "Objective",
            _ => type
        };

        private static IEnumerable<QuestCondition> Concat(
            List<QuestCondition>? a, List<QuestCondition>? b)
        {
            foreach (var c in a ?? [])
                yield return c;
            foreach (var c in b ?? [])
                yield return c;
        }

        private static IEnumerable<string> ReadTargets(ListOrT<string>? target)
        {
            if (target is null)
                yield break;

            if (target.IsList && target.List is not null)
            {
                foreach (var value in target.List)
                    yield return value;
            }
            else if (target.IsItem && target.Item is not null)
            {
                yield return target.Item;
            }
        }

        private string ResolveQuestName(string questId, Quest quest)
        {
            var fromLocale = ResolveLocale($"{questId} name");
            if (!string.IsNullOrWhiteSpace(fromLocale))
                return fromLocale;

            return string.IsNullOrWhiteSpace(quest.QuestName)
                ? quest.Name ?? questId
                : quest.QuestName;
        }

        /// <summary>
        /// Turns a quest's raw <c>Location</c> (a map template id, or "any") into a
        /// display name via the locale DB. Empty string when the quest isn't tied to
        /// a specific map.
        /// </summary>
        private string ResolveMapName(string? location)
        {
            if (string.IsNullOrWhiteSpace(location) ||
                location.Equals("any", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return ResolveLocale($"{location} Name");
        }

        private string ResolveLocale(string key)
        {
            _localeCache ??= LoadLocale();
            return _localeCache.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private Dictionary<string, string> LoadLocale()
        {
            try
            {
                var db = _localeService.GetLocaleDb(_language);
                if (db is { Count: > 0 })
                    return db;

                if (_language != DefaultLanguage)
                {
                    _logger.Warning($"[KappaTracker] Locale '{_language}' unavailable; falling back to '{DefaultLanguage}'");
                    db = _localeService.GetLocaleDb(DefaultLanguage);
                    if (db is { Count: > 0 })
                        return db;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[KappaTracker] Failed to load locale DB", ex);
            }

            return new Dictionary<string, string>();
        }
    }
}
