using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Quests;
using HarmonyLib;
using UnityEngine;

namespace MissionControl.Client
{
    [BepInPlugin("com.chazut.missioncontrol.client", "MissionControl Client", "1.0.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance;

        // Must match RerollService.RerollItemId on the server
        internal const string RerollItemTemplateId = "a0b1c2d3e4f5000000000001";

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            new Harmony("com.chazut.missioncontrol.client").PatchAll();
            Logger.LogInfo("MissionControl.Client loaded");
        }

        private float _checkInterval = 0;
        private bool _logOnce = true;
        private bool _rerollDetected = false;

        private void Update()
        {
            if (_rerollDetected) return; // already processing a reroll
            _checkInterval -= Time.deltaTime;
            if (_checkInterval > 0) return;
            _checkInterval = 0.5f;

            try
            {
                var session = Singleton<ClientApplication<ISession>>.Instance?.GetClientBackEndSession();
                if (session?.Profile?.Inventory == null) return;

                int count = 0;
                foreach (var item in session.Profile.Inventory.AllRealPlayerItems)
                {
                    count++;
                    if (item.TemplateId.ToString() == RerollItemTemplateId)
                    {
                        Log.LogInfo("[MissionControl] Reroll item detected — removing and reloading profile");
                        // Remove the item from client inventory to prevent re-detection
                        try
                        {
                            var stash = session.Profile.Inventory.Stash;
                            if (stash != null)
                            {
                                var grid = stash.Grids[0];
                                grid.Remove(item, false);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.LogWarning($"[MissionControl] Could not remove reroll item: {ex.Message}");
                        }
                        _rerollDetected = true;
                        StartCoroutine(ReloadMainMenu());
                        return;
                    }
                }

                // Log once to confirm polling works
                if (_logOnce)
                {
                    Log.LogInfo($"[MissionControl] Inventory polling active ({count} items scanned)");
                    _logOnce = false;
                }
            }
            catch (Exception ex)
            {
                if (_logOnce)
                {
                    Log.LogError($"[MissionControl] Polling error: {ex.Message}");
                    _logOnce = false;
                }
            }
        }

        /// <summary>
        /// Force the main menu to reload the profile, which re-requests quest data.
        /// Same approach as QuestsExtended: call MainMenuControllerClass.method_5().
        /// </summary>
        private IEnumerator ReloadMainMenu()
        {
            yield return new WaitForSeconds(0.3f);

            bool success = false;
            try
            {
                var app = Singleton<ClientApplication<ISession>>.Instance;
                if (app != null)
                {
                    foreach (var field in app.GetType().GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        var val = field.GetValue(app);
                        if (val == null) continue;

                        var method = val.GetType().GetMethod("method_5",
                            BindingFlags.Public | BindingFlags.Instance,
                            null, Type.EmptyTypes, null);
                        if (method == null) continue;

                        var qcField = val.GetType().GetField("LocalQuestControllerClass",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (qcField == null) continue;

                        Log.LogInfo("[MissionControl] Reloading profile via method_5()");
                        method.Invoke(val, null);
                        success = true;
                        break;
                    }
                }
                if (!success)
                    Log.LogWarning("[MissionControl] MainMenuControllerClass not found");
            }
            catch (Exception ex)
            {
                Log.LogError($"[MissionControl] Profile reload failed: {ex.Message}");
            }

            // Wait before allowing re-detection for future rerolls
            yield return new WaitForSeconds(5f);
            _rerollDetected = false;
        }
    }

    public static class QuestRefresh
    {
        public static void ApplyTemplates(LocalQuestControllerClass controller, List<RawQuestClass> templates)
        {
            var questBook = controller.Quests;
            if (questBook == null) return;

            var newIds = new HashSet<string>();
            foreach (var t in templates)
            {
                if (t.Id != null)
                    newIds.Add(t.Id.ToString());
            }

            var toRemove = new List<QuestClass>();
            foreach (var quest in questBook)
            {
                if (quest.QuestStatus == EQuestStatus.AvailableForStart &&
                    !newIds.Contains(quest.Id.ToString()))
                {
                    toRemove.Add(quest);
                }
            }
            foreach (var quest in toRemove)
                questBook.RemoveQuestTemplate(quest.Template);

            questBook.AddTemplates(templates);

            var eventField = typeof(AbstractQuestControllerClass)
                .GetField("Action_0", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (eventField?.GetValue(controller) is Action onStatusChanged)
                onStatusChanged.Invoke();

            Plugin.Log.LogInfo($"[MissionControl] Refreshed ({templates.Count} templates, removed {toRemove.Count})");
        }
    }

    /// <summary>
    /// After quest completion, refresh quest list.
    /// </summary>
    [HarmonyPatch(typeof(LocalQuestControllerClass), nameof(LocalQuestControllerClass.SetConditionalStatus))]
    public static class SetConditionalStatus_Patch
    {
        public static void Postfix(LocalQuestControllerClass __instance, QuestClass quest, EQuestStatus status)
        {
            if (status == EQuestStatus.Success)
            {
                Plugin.Instance?.StartCoroutine(RefreshAfterCompletion(__instance));
            }
        }

        private static IEnumerator RefreshAfterCompletion(LocalQuestControllerClass controller)
        {
            yield return new WaitForSeconds(0.5f);

            var questActions = controller.IQuestActions;
            if (questActions == null) yield break;

            var task = questActions.RequestQuestsTemplates(true);
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted) yield break;

            var templates = task.Result;
            if (templates == null || templates.Count == 0) yield break;

            QuestRefresh.ApplyTemplates(controller, templates);
        }
    }
}
