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
    [BepInPlugin("com.chazut.missioncontrol.client", "MissionControl Client", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            new Harmony("com.chazut.missioncontrol.client").PatchAll();
            Logger.LogInfo("MissionControl.Client loaded");
        }
    }

    public static class QuestRefresh
    {
        public static LocalQuestControllerClass FindController()
        {
            try
            {
                var app = Singleton<ClientApplication<ISession>>.Instance;
                if (app == null) return null;

                foreach (var field in app.GetType().GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var val = field.GetValue(app);
                    if (val == null) continue;

                    var qcField = val.GetType().GetField("LocalQuestControllerClass",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (qcField?.GetValue(val) is LocalQuestControllerClass qc)
                        return qc;
                }
            }
            catch { }
            return null;
        }

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
    /// After quest completion, refresh quest list via coroutine (correct Unity thread context).
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
