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
    }
}
