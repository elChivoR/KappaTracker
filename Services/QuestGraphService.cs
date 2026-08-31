using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Quest;

namespace KappaTracker.Services
{
    public readonly record struct PrereqEdgeInfo(string QuestId, string? GateNote);

    public sealed record QuestGraphNode(string Id, IReadOnlyList<string> PrereqIds, string? GateNote);

    [Injectable(InjectionType.Singleton)]
    public class QuestGraphService
    {
        private readonly ISptLogger<QuestGraphService> _logger;
        private readonly QuestHelper _questHelper;

        private IReadOnlyDictionary<string, QuestGraphNode>? _graph;
        private readonly Dictionary<string, IReadOnlyList<PrereqEdgeInfo>> _chainCache = new();

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

            var graph = _graph ??= BuildGraph();
            var order = BuildAncestorOrder(questId, graph);

            var chain = order
                .Select(id => new PrereqEdgeInfo(
                    id,
                    graph.TryGetValue(id, out var node) ? node.GateNote : null))
                .ToList();

            _chainCache[questId] = chain;
            return chain;
        }

        private IReadOnlyDictionary<string, QuestGraphNode> BuildGraph()
        {
            var graph = new Dictionary<string, QuestGraphNode>();

            foreach (var quest in _questHelper.GetQuestsFromDb())
            {
                var id = (string)quest.Id;
                var prereqs = new List<string>();
                var gates = new List<string>();

                foreach (var cond in quest.Conditions?.AvailableForStart ?? [])
                {
                    var type = !string.IsNullOrWhiteSpace(cond.ConditionType)
                        ? cond.ConditionType!
                        : cond.Type ?? string.Empty;

                    switch (type)
                    {
                        case "Quest":
                            foreach (var target in ReadTargets(cond.Target))
                                prereqs.Add(target);
                            break;

                        case "Level":
                            if (cond.Value is > 0)
                                gates.Add($"Lv {(int)cond.Value.Value}");
                            break;

                        case "TraderLoyalty":
                            var trader = TraderNames.GetValueOrDefault(cond.TraderId ?? string.Empty, "trader");
                            if (cond.Value is > 0)
                                gates.Add($"LL {(int)cond.Value.Value} {trader}");
                            break;
                    }
                }

                graph[id] = new QuestGraphNode(
                    id,
                    prereqs,
                    gates.Count > 0 ? string.Join(" · ", gates.Distinct()) : null);
            }

            _logger.Success($"[KappaTracker] Prereq graph built: {graph.Count} quests");
            return graph;
        }

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
