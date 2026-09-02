using System;
using System.Collections.Generic;

namespace KappaTracker.Client
{
    /// <summary>
    /// Holds the Kappa milestone quest-id set for this game session and decides how a
    /// task-list row's name string is decorated. Pure logic — no BepInEx or Unity types,
    /// so it is unit-tested directly.
    /// </summary>
    public static class KappaTagService
    {
        public const string Prefix = "KAPPA · ";

        private static HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);

        public static bool Ready { get; private set; }

        public static void SetIds(IEnumerable<string> ids)
        {
            if (ids is null) throw new ArgumentNullException(nameof(ids));
            _ids = new HashSet<string>(ids, StringComparer.Ordinal);
            Ready = true;
        }

        public static void Reset()
        {
            _ids = new HashSet<string>(StringComparer.Ordinal);
            Ready = false;
        }

        public static bool IsKappa(string templateId)
            => Ready && !string.IsNullOrEmpty(templateId) && _ids.Contains(templateId);

        public static string Decorate(string templateId, string name)
        {
            if (!Ready || string.IsNullOrEmpty(name)) return name;
            if (!IsKappa(templateId)) return name;
            if (name.StartsWith(Prefix, StringComparison.Ordinal)) return name;
            return Prefix + name;
        }
    }
}
