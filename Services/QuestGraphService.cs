using System.Collections.Concurrent;
using KappaTracker.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Enums;

namespace KappaTracker.Services
{
    public readonly record struct PrereqEdgeInfo(
        string QuestId,
        string? GateNote,
        string? QuestName,
        IReadOnlyCollection<KappaQuestStatus> SatisfyingStatuses);

    public sealed record QuestGraphNode(
        string Id,
        IReadOnlyList<string> PrereqIds,
        string? GateNote,
        string? QuestName,
        IReadOnlyCollection<KappaQuestStatus> SatisfyingStatuses);

    [Injectable(InjectionType.Singleton)]
    public class QuestGraphService
    {
        private readonly ISptLogger<QuestGraphService> _logger;
        private readonly QuestHelper _questHelper;

        private readonly object _graphLock = new();
        private volatile IReadOnlyDictionary<string, QuestGraphNode>? _graph;
        private readonly ConcurrentDictionary<string, IReadOnlyList<PrereqEdgeInfo>> _chainCache = new();

        public QuestGraphService(ISptLogger<QuestGraphService> logger, QuestHelper questHelper)
        {
            _logger = logger;
            _questHelper = questHelper;
        }

        /// <summary>
        /// Every transitive prerequisite quest id of <paramref name="questId"/> (not
        /// including it), ordered so a prerequisite always precedes anything that needs
        /// it. De-duplicated, cycle-safe, unknown ids skipped.
        /// </summary>
        public static IReadOnlyList<string> BuildAncestorOrder(
            string questId, IReadOnlyDictionary<string, QuestGraphNode> graph)
        {
            var order = new List<string>();
            var state = new Dictionary<string, byte>(); // 1 = visiting, 2 = done

            void Visit(string id)
            {
                if (state.ContainsKey(id))
                    return; // visiting or done -> skip (cycle back-edge or already emitted)

                state[id] = 1;
                bool known = graph.TryGetValue(id, out var node);
                if (known)
                {
                    foreach (var prereq in node!.PrereqIds)
                        Visit(prereq);
                }
                state[id] = 2;
                if (known && id != questId)
                    order.Add(id);
            }

            Visit(questId);
            return order;
        }

        private static readonly Dictionary<string, string> TraderNames = new()
        {
            ["54cb50c76803fa8b248b4571"] = "Prapor",
            ["54cb57776803fa99248b456e"] = "Therapist",
            ["579dc571d53a0658a154fbec"] = "Fence",
            ["58330581ace78e27b8b10cee"] = "Skier",
            ["5935c25fb3acc3127c3d8cd9"] = "Peacekeeper",
            ["5a7c2eca46aef81a7ca2145d"] = "Mechanic",
            ["5ac3b934156ae10c4430e83c"] = "Ragman",
            ["5c0647fdd443bc2504c2d371"] = "Jaeger",
            ["638f541a29ffd1183d187f57"] = "Lightkeeper",
            ["656f0f98d80a697f855d34b1"] = "BTR",
            ["6617beeaa9cfa777ca915b7c"] = "Ref"
        };

        public IReadOnlyList<PrereqEdgeInfo> GetAncestorChain(string questId)
        {
            if (_chainCache.TryGetValue(questId, out var cached))
                return cached;

            var graph = GetGraph();
            var order = BuildAncestorOrder(questId, graph);

            var chain = order
                .Select(id =>
                {
                    graph.TryGetValue(id, out var node);
                    return new PrereqEdgeInfo(
                        id,
                        node?.GateNote,
                        node?.QuestName,
                        node?.SatisfyingStatuses ?? new[] { KappaQuestStatus.Completed });
                })
                .ToList()
                .AsReadOnly();

            _chainCache.TryAdd(questId, chain);
            return chain;
        }

        private IReadOnlyDictionary<string, QuestGraphNode> GetGraph()
        {
            if (_graph is not null)
                return _graph;
            lock (_graphLock)
            {
                return _graph ??= BuildGraph();
            }
        }

        private IReadOnlyDictionary<string, QuestGraphNode> BuildGraph()
        {
            var prereqsByQuest = new Dictionary<string, List<string>>();
            var gatesByQuest = new Dictionary<string, List<string>>();
            var nameByQuest = new Dictionary<string, string?>();
            var satisfyingByQuest = new Dictionary<string, HashSet<KappaQuestStatus>>();

            var quests = _questHelper.GetQuestsFromDb();

            foreach (var quest in quests)
            {
                var id = (string)quest.Id;
                nameByQuest[id] = string.IsNullOrWhiteSpace(quest.QuestName) ? quest.Name : quest.QuestName;
                var prereqs = prereqsByQuest.TryGetValue(id, out var pl) ? pl : (prereqsByQuest[id] = new());
                var gates = gatesByQuest.TryGetValue(id, out var gl) ? gl : (gatesByQuest[id] = new());

                foreach (var cond in quest.Conditions?.AvailableForStart ?? [])
                {
                    var type = !string.IsNullOrWhiteSpace(cond.ConditionType)
                        ? cond.ConditionType!
                        : cond.Type ?? string.Empty;

                    switch (type)
                    {
                        case "Quest":
                            var mapped = MapConditionStatuses(cond.Status);
                            foreach (var target in ReadTargets(cond.Target))
                            {
                                prereqs.Add(target);
                                var set = satisfyingByQuest.TryGetValue(target, out var ss)
                                    ? ss : (satisfyingByQuest[target] = new());
                                foreach (var m in mapped)
                                    set.Add(m);
                            }
                            break;

                        case "Level":
                            if (cond.Value is > 0)
                                gates.Add($"Lv {(int)cond.Value.Value}");
                            break;

                        case "TraderLoyalty":
                            // trader id lives in Target, not TraderId (null in the DB)
                            var traderId = ReadTargets(cond.Target).FirstOrDefault()
                                           ?? cond.TraderId ?? string.Empty;
                            var trader = TraderNames.TryGetValue(traderId, out var tn)
                                ? tn
                                : (string.IsNullOrEmpty(traderId) ? "Trader" : traderId);
                            if (cond.Value is > 0)
                                gates.Add($"LL {(int)cond.Value.Value} {trader}");
                            break;
                    }
                }
            }

            var graph = new Dictionary<string, QuestGraphNode>(prereqsByQuest.Count);
            foreach (var (id, prereqs) in prereqsByQuest)
            {
                var gates = gatesByQuest[id];
                satisfyingByQuest.TryGetValue(id, out var satRaw);
                var satisfying = NormalizeSatisfying(satRaw);
                graph[id] = new QuestGraphNode(
                    id,
                    prereqs,
                    gates.Count > 0 ? string.Join(" · ", gates.Distinct()) : null,
                    nameByQuest.GetValueOrDefault(id),
                    satisfying);
            }

            _logger.Success($"[KappaTracker] Prereq graph built: {graph.Count} quests");
            return graph;
        }

        /// <summary>Maps a condition's allowed statuses to our enum; empty/null -> {Completed}.</summary>
        private static IReadOnlyCollection<KappaQuestStatus> MapConditionStatuses(
            IReadOnlyCollection<QuestStatusEnum>? statuses)
        {
            if (statuses is null || statuses.Count == 0)
                return new[] { KappaQuestStatus.Completed };
            var set = new HashSet<KappaQuestStatus>();
            foreach (var s in statuses)
                set.Add(MapRawStatus(s));
            return set;
        }

        /// <summary>
        /// A prereq whose gate accepts "Started" is also satisfied once you've gone further
        /// (ReadyToHandIn / Completed); one that accepts "ReadyToHandIn" is satisfied when Completed.
        /// Completed always satisfies unless the gate is Failed-only. Never returns empty.
        /// </summary>
        private static IReadOnlyCollection<KappaQuestStatus> NormalizeSatisfying(HashSet<KappaQuestStatus>? raw)
        {
            var set = raw is { Count: > 0 } ? new HashSet<KappaQuestStatus>(raw) : new HashSet<KappaQuestStatus>();
            if (set.Count == 0)
                set.Add(KappaQuestStatus.Completed);
            if (set.Contains(KappaQuestStatus.InProgress))
            {
                set.Add(KappaQuestStatus.ReadyToHandIn);
                set.Add(KappaQuestStatus.Completed);
            }
            if (set.Contains(KappaQuestStatus.ReadyToHandIn))
                set.Add(KappaQuestStatus.Completed);
            return set;
        }

        private static KappaQuestStatus MapRawStatus(QuestStatusEnum s) => s switch
        {
            QuestStatusEnum.Success => KappaQuestStatus.Completed,
            QuestStatusEnum.Started => KappaQuestStatus.InProgress,
            QuestStatusEnum.AvailableForFinish => KappaQuestStatus.ReadyToHandIn,
            QuestStatusEnum.AvailableForStart or QuestStatusEnum.AvailableAfter => KappaQuestStatus.Available,
            QuestStatusEnum.Fail or QuestStatusEnum.FailRestartable
                or QuestStatusEnum.MarkedAsFailed or QuestStatusEnum.Expired => KappaQuestStatus.Failed,
            _ => KappaQuestStatus.Locked
        };

        private static IEnumerable<string> ReadTargets(SPTarkov.Server.Core.Utils.Json.ListOrT<string>? target)
        {
            if (target is null)
                yield break;
            if (target.IsList && target.List is not null)
                foreach (var value in target.List)
                    yield return value;
            else if (target.IsItem && target.Item is not null)
                yield return target.Item;
        }
    }
}
