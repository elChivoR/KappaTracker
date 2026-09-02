using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace KappaTracker.Client
{
    internal static class MilestoneClient
    {
        private const string Route = "/kappa/api/milestones";

        public static void Fetch()
        {
            if (KappaTagService.Ready) return;
            try
            {
                var json = RequestHandler.GetJson(Route);
                if (string.IsNullOrEmpty(json))
                {
                    Plugin.Log.LogWarning("KappaTracker: milestones response was empty; tags disabled this session.");
                    return;
                }

                var root = JObject.Parse(json);
                var ids = root["templateIds"]?.Values<string>()
                              .Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToArray()
                          ?? Array.Empty<string>();
                if (ids.Length == 0)
                {
                    Plugin.Log.LogWarning("KappaTracker: milestones response had no templateIds; tags disabled this session.");
                    return;
                }

                KappaTagService.SetIds(ids);

                var serverVersion = (string?)root["modVersion"];
                if (!string.IsNullOrEmpty(serverVersion) && serverVersion != ModInfo.Version)
                    Plugin.Log.LogWarning($"KappaTracker: server mod {serverVersion} vs client {ModInfo.Version} (still fine).");

                Plugin.Log.LogInfo($"KappaTracker: {ids.Length} milestone ids loaded.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"KappaTracker: could not load milestones ({ex.GetType().Name}: {ex.Message}); tags disabled this session.");
            }
        }
    }
}
