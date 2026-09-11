using System;
using System.Reflection;
using HarmonyLib;

namespace KappaTracker.Client
{
    // Injects a "K" (Kappa-only) toggle into the trader task list filter bar.
    // Clones _toggleShowCompleted so it matches the native look.
    // Intercepts clicks via a Toggle:Set patch (onValueChanged is null on clones).
    internal static class TraderKappaTogglePatch
    {
        private static bool _kappaOnly;
        private static WeakReference? _activeView;
        private static bool _loggedShow;
        private static bool _loggedUpdate;
        private static MethodInfo? _getComponentsInChildren;

        private const string ToggleName = "KappaTracker_KappaToggle";

        public static void Patch(Harmony h)
        {
            var showTarget = AccessTools.Method("EFT.UI.QuestsListView:Show");
            Plugin.Log.LogInfo($"KappaTracker: QuestsListView.Show method found={showTarget != null}");
            if (showTarget != null)
                h.Patch(showTarget, postfix: new HarmonyMethod(typeof(TraderKappaTogglePatch), nameof(PostfixShow)));

            var updateTarget = AccessTools.Method("EFT.UI.QuestsListView:UpdateVisibility");
            Plugin.Log.LogInfo($"KappaTracker: QuestsListView.UpdateVisibility method found={updateTarget != null}");
            if (updateTarget != null)
                h.Patch(updateTarget, postfix: new HarmonyMethod(typeof(TraderKappaTogglePatch), nameof(PostfixUpdate)));

            // onValueChanged is null on cloned Toggles — intercept clicks via Toggle:Set instead
            var setTarget = AccessTools.Method("UnityEngine.UI.Toggle:Set",
                new[] { typeof(bool), typeof(bool) });
            if (setTarget == null)
                setTarget = AccessTools.Method("UnityEngine.UI.Toggle:set_isOn");
            if (setTarget != null)
                h.Patch(setTarget, postfix: new HarmonyMethod(typeof(TraderKappaTogglePatch), nameof(PostfixToggleSet)));
            else
                Plugin.Log.LogWarning("KappaTracker: Toggle:Set not found — Kappa toggle won't filter");
        }

        private static void PostfixShow(object __instance)
        {
            try
            {
                if (!KappaConfig.Enabled.Value) return;

                _activeView = new WeakReference(__instance);
                _kappaOnly = false;

                var qvType = AccessTools.TypeByName("EFT.UI.QuestsListView");
                var sourceToggle = AccessTools.Field(qvType, "_toggleShowCompleted")?.GetValue(__instance);
                if (sourceToggle == null) { Plugin.Log.LogWarning("KappaTracker: _toggleShowCompleted not found"); return; }

                var sourceGO = Traverse.Create(sourceToggle).Property("gameObject").GetValue();
                if (sourceGO == null) { Plugin.Log.LogWarning("KappaTracker: sourceGO null"); return; }

                var parent = Traverse.Create(sourceGO).Property("transform").Property("parent").GetValue();
                if (parent == null) { Plugin.Log.LogWarning("KappaTracker: parent null"); return; }

                var childCount = Traverse.Create(parent).Property("childCount").GetValue<int>();
                float parentW = Traverse.Create(Traverse.Create(parent).Property("rect").GetValue())
                    .Property("width").GetValue<float>();

                // Skip if already injected
                if (Traverse.Create(parent).Method("Find", ToggleName).GetValue() != null)
                    return;

                // Clone the toggle
                var objType = AccessTools.TypeByName("UnityEngine.Object");
                var clone = AccessTools.Method(objType, "Instantiate", new[] { objType })
                    ?.Invoke(null, new object[] { sourceGO });
                if (clone == null) { Plugin.Log.LogWarning("KappaTracker: clone null"); return; }

                Traverse.Create(clone).Property("name").SetValue(ToggleName);

                // Re-parent last (highest sibling = drawn on top of the Completed counter)
                var cloneXform = Traverse.Create(clone).Property("transform").GetValue();
                Traverse.Create(cloneXform).Method("SetParent", parent, false).GetValue();

                // ToggleShowLocked (child[2]) right edge = pos.x + rect.width = 404
                var lockedXform = Traverse.Create(parent)
                    .Method("GetChild", new Type[] { typeof(int) }, new object[] { 2 }).GetValue();
                var lockedPos = Traverse.Create(lockedXform).Property("anchoredPosition").GetValue();
                float lockedX = Traverse.Create(lockedPos).Field("x").GetValue<float>();
                float lockedY = Traverse.Create(lockedPos).Field("y").GetValue<float>();
                float lockedW = Traverse.Create(lockedXform).Property("rect").Property("width").GetValue<float>();
                float lockedRightEdge = lockedX + lockedW;

                // Completed counter (child[childCount-1], still at index 3 before our clone)
                // right-anchored → left edge ≈ parentW - counterWidth
                var counterXform = Traverse.Create(parent)
                    .Method("GetChild", new Type[] { typeof(int) }, new object[] { childCount - 1 }).GetValue();
                float counterW = Traverse.Create(counterXform).Property("rect").Property("width").GetValue<float>();
                float counterLeftEdge = parentW - counterW;

                var vector2Type = AccessTools.TypeByName("UnityEngine.Vector2");
                float ourW = System.Math.Max(counterLeftEdge - lockedRightEdge, 40f);
                Traverse.Create(cloneXform).Property("anchoredPosition").SetValue(
                    Activator.CreateInstance(vector2Type, lockedRightEdge, lockedY));

                var curSize = Traverse.Create(cloneXform).Property("sizeDelta").GetValue();
                float sdY = Traverse.Create(curSize).Field("y").GetValue<float>();
                Traverse.Create(cloneXform).Property("sizeDelta").SetValue(
                    Activator.CreateInstance(vector2Type, ourW, sdY));

                // Label
                var tmpType = AccessTools.TypeByName("TMPro.TMP_Text");
                var label = Traverse.Create(clone)
                    .Method("GetComponentInChildren", new Type[] { typeof(Type), typeof(bool) },
                        new object[] { tmpType, true })
                    .GetValue();
                if (label != null)
                {
                    // Disable EFT's localization component so it doesn't reset our text
                    var labelGO = Traverse.Create(label).Property("gameObject").GetValue();
                    if (labelGO != null)
                    {
                        foreach (var locTypeName in new[] { "EFT.UI.LocalizedText", "LocalizedText" })
                        {
                            var lt = AccessTools.TypeByName(locTypeName);
                            if (lt == null) continue;
                            var locComp = Traverse.Create(labelGO)
                                .Method("GetComponent", new Type[] { typeof(Type) }, new object[] { lt })
                                .GetValue();
                            if (locComp != null)
                            {
                                Traverse.Create(locComp).Property("enabled").SetValue(false);
                                break;
                            }
                        }
                    }
                    Traverse.Create(label).Property("text").SetValue("K");
                    Traverse.Create(label).Property("enableWordWrapping").SetValue(false);
                    Traverse.Create(label).Property("overflowMode").SetValue(0);
                }

                // Reset toggle state (onValueChanged is null on clones — clicks caught by Toggle:Set patch)
                var toggleType = AccessTools.TypeByName("UnityEngine.UI.Toggle");
                var toggle = Traverse.Create(clone)
                    .Method("GetComponent", new Type[] { typeof(Type) }, new object[] { toggleType })
                    .GetValue();
                if (toggle != null)
                    Traverse.Create(toggle).Property("isOn").SetValue(false);

                Plugin.Log.LogInfo("KappaTracker: Show Kappa toggle injected successfully");
            }
            catch (Exception ex)
            {
                if (_loggedShow) return;
                _loggedShow = true;
                Plugin.Log.LogError(
                    $"KappaTracker: TraderKappaToggle.Show failed ({ex.GetType().Name}: {ex.Message})\n{ex.StackTrace}");
            }
        }

        // Fired for EVERY Toggle:Set call — filter by our toggle's name
        private static void PostfixToggleSet(object __instance)
        {
            try
            {
                var go = Traverse.Create(__instance).Property("gameObject").GetValue();
                if (go == null) return;
                var name = Traverse.Create(go).Property("name").GetValue<string>();
                if (name != ToggleName) return;

                bool isOn = Traverse.Create(__instance).Property("isOn").GetValue<bool>();
                Plugin.Log.LogInfo($"KappaTracker: Kappa toggle set isOn={isOn}");
                _kappaOnly = isOn;

                var view = _activeView?.Target;
                if (view != null)
                    Traverse.Create(view).Method("UpdateVisibility").GetValue();
            }
            catch { }
        }

        private static void PostfixUpdate(object __instance)
        {
            try
            {
                if (!_kappaOnly || !KappaConfig.Enabled.Value || !KappaTagService.Ready) return;

                var rows = GetRows(__instance);
                if (rows == null) return;

                foreach (var row in rows)
                {
                    if (row == null) continue;
                    var quest = Traverse.Create(row).Field("Quest").GetValue();
                    string? templateId = quest != null
                        ? Traverse.Create(quest).Property("Template").Property("Id").GetValue<string>()
                        : null;
                    bool isKappa = !string.IsNullOrEmpty(templateId) && KappaTagService.IsKappa(templateId!);
                    if (!isKappa)
                    {
                        var go = Traverse.Create(row).Property("gameObject").GetValue();
                        if (go != null)
                            Traverse.Create(go).Method("SetActive", false).GetValue();
                    }
                }
            }
            catch (Exception ex)
            {
                if (_loggedUpdate) return;
                _loggedUpdate = true;
                Plugin.Log.LogError(
                    $"KappaTracker: TraderKappaToggle.Update failed ({ex.GetType().Name}: {ex.Message})");
            }
        }

        private static Array? GetRows(object view)
        {
            if (_getComponentsInChildren == null)
            {
                var ct = AccessTools.TypeByName("UnityEngine.Component");
                if (ct == null) return null;
                _getComponentsInChildren = AccessTools.Method(
                    ct, "GetComponentsInChildren", new[] { typeof(Type), typeof(bool) });
            }
            if (_getComponentsInChildren == null) return null;
            var rowType = AccessTools.TypeByName("EFT.UI.QuestListItem");
            if (rowType == null) return null;
            return (Array?)_getComponentsInChildren.Invoke(view, new object[] { rowType, false });
        }
    }
}
