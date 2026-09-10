using System;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;

namespace KappaTracker.Client
{
    // Optional integration with TaskSearch (https://github.com/hjal-dev/Task-Search).
    // Typing "kappa" into any TaskSearch bar overrides the result set to show only Kappa
    // milestone quests. Silently skipped when TaskSearch is not installed.
    // Two surfaces: trader dialogue (TraderTaskSearchController) and Character > Tasks (TaskSearchController).
    internal static class KappaSearchFilterPatch
    {
        private static bool _loggedError;
        private static MethodInfo? _getComponentsInChildren;
        private static MethodInfo? _questViewListFilter;
        private static Delegate? _kappaFilter;

        public static void Patch(Harmony h)
        {
            bool any = false;

            var traderTarget = AccessTools.Method("TaskSearch.UI.TraderTaskSearchController:OnVisibilityUpdated");
            if (traderTarget != null)
            {
                h.Patch(traderTarget, postfix: new HarmonyMethod(typeof(KappaSearchFilterPatch), nameof(PostfixTrader)));
                any = true;
            }

            var panelTarget = AccessTools.Method("TaskSearch.UI.TaskSearchController:ApplyQuery");
            if (panelTarget != null)
            {
                h.Patch(panelTarget, postfix: new HarmonyMethod(typeof(KappaSearchFilterPatch), nameof(PostfixTasksPanel)));
                any = true;
            }

            if (any)
                Plugin.Log.LogInfo("KappaTracker: TaskSearch integration active.");
        }

        // ── Trader dialogue (QuestsListView rows) ────────────────────────────
        private static void PostfixTrader(object __instance)
        {
            try
            {
                if (!KappaConfig.Enabled.Value || !KappaTagService.Ready) return;

                var field = Traverse.Create(__instance).Field("_field").GetValue();
                if (field == null) return;

                var text = Traverse.Create(field).Property("text").GetValue<string>();
                if (!IsKappaOnly(text)) return;

                var view = Traverse.Create(__instance).Field("_view").GetValue();
                if (view == null) return;

                var rows = GetRows(view, "EFT.UI.QuestListItem");
                if (rows == null) return;

                foreach (var row in rows)
                {
                    if (row == null) continue;
                    var quest = Traverse.Create(row).Field("Quest").GetValue();
                    string? templateId = quest != null
                        ? Traverse.Create(quest).Property("Template").Property("Id").GetValue<string>()
                        : null;
                    bool isKappa = !string.IsNullOrEmpty(templateId) && KappaTagService.IsKappa(templateId!);
                    var go = Traverse.Create(row).Property("gameObject").GetValue();
                    if (go != null)
                        Traverse.Create(go).Method("SetActive", isKappa).GetValue();
                }
            }
            catch (Exception ex) { LogOnce("PostfixTrader", ex); }
        }

        // ── Character > Tasks (QuestViewList.Filter) ─────────────────────────
        private static void PostfixTasksPanel(object __instance)
        {
            try
            {
                if (!KappaConfig.Enabled.Value || !KappaTagService.Ready) return;

                var field = Traverse.Create(__instance).Field("_field").GetValue();
                if (field == null) return;

                var text = Traverse.Create(field).Property("text").GetValue<string>();
                if (!IsKappaOnly(text)) return;

                var viewList = Traverse.Create(__instance).Field("_viewList").GetValue();
                if (viewList == null) return;

                if (_questViewListFilter == null)
                {
                    var t = AccessTools.TypeByName("TaskSearch.Eft.QuestViewList");
                    _questViewListFilter = t != null ? AccessTools.Method(t, "Filter") : null;
                    if (_questViewListFilter == null) return;
                }

                var questType = AccessTools.TypeByName("EFT.Quests.Quest");
                if (questType == null) return;

                if (_kappaFilter == null)
                    _kappaFilter = BuildKappaFilter(questType);

                _questViewListFilter!.Invoke(viewList, new object[] { _kappaFilter });

                // TaskSearch sets "NO ACTIVE TASKS" visible when its own search finds nothing.
                // Hide it — we have results, just not text-matched ones.
                var noResults = Traverse.Create(__instance).Field("_noResultsObject").GetValue();
                if (noResults != null)
                    Traverse.Create(noResults).Method("SetActive", false).GetValue();
            }
            catch (Exception ex) { LogOnce("PostfixTasksPanel", ex); }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool IsKappaOnly(string? text) =>
            !string.IsNullOrEmpty(text) &&
            text!.Trim().Equals("kappa", StringComparison.OrdinalIgnoreCase);

        // Builds Func<Quest, bool> via Expression so we never reference Quest directly.
        private static Delegate BuildKappaFilter(Type questType)
        {
            var param = Expression.Parameter(questType, "q");
            var boxed = Expression.Convert(param, typeof(object));
            var method = typeof(KappaSearchFilterPatch).GetMethod(
                nameof(IsKappaQuestObject), BindingFlags.NonPublic | BindingFlags.Static)!;
            var funcType = typeof(Func<,>).MakeGenericType(questType, typeof(bool));
            return Expression.Lambda(funcType, Expression.Call(method, boxed), param).Compile();
        }

        private static bool IsKappaQuestObject(object quest)
        {
            if (quest == null) return false;
            try
            {
                var templateId = Traverse.Create(quest)
                    .Property("Template").Property("Id").GetValue<string>();
                return !string.IsNullOrEmpty(templateId) && KappaTagService.IsKappa(templateId!);
            }
            catch { return false; }
        }

        private static Array? GetRows(object component, string typeName)
        {
            if (_getComponentsInChildren == null)
            {
                var ct = AccessTools.TypeByName("UnityEngine.Component");
                if (ct == null) return null;
                _getComponentsInChildren = AccessTools.Method(
                    ct, "GetComponentsInChildren", new[] { typeof(Type), typeof(bool) });
            }
            if (_getComponentsInChildren == null) return null;
            var rowType = AccessTools.TypeByName(typeName);
            if (rowType == null) return null;
            return (Array?)_getComponentsInChildren.Invoke(component, new object[] { rowType, true });
        }

        private static void LogOnce(string where, Exception ex)
        {
            if (_loggedError) return;
            _loggedError = true;
            Plugin.Log.LogError(
                $"KappaTracker: KappaSearchFilter.{where} failed ({ex.GetType().Name}: {ex.Message}); suppressing further errors.");
        }
    }
}
