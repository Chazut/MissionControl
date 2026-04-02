using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using EFT.Quests;
using HarmonyLib;

namespace MissionControl.Client
{
    [BepInPlugin("com.chazut.missioncontrol.client", "MissionControl Client", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            new Harmony("com.chazut.missioncontrol.client").PatchAll();
            Logger.LogInfo("MissionControl.Client loaded — quest list will refresh after completion.");
        }
    }

    /// <summary>
    /// After a quest is completed successfully, re-request the quest list from the server
    /// and update the quest book. This ensures MissionControl's filtered list (with the
    /// new random replacement quest) is applied immediately.
    /// </summary>
    [HarmonyPatch(typeof(LocalQuestControllerClass), nameof(LocalQuestControllerClass.FinishQuest))]
    public static class FinishQuest_RefreshQuestList_Patch
    {
        public static void Postfix(LocalQuestControllerClass __instance, Task __result)
        {
            // Wait for the completion task to finish, then refresh
            RefreshAfterCompletion(__instance, __result).ContinueWith(t =>
            {
                if (t.Exception != null)
                    Plugin.Log.LogError($"[MissionControl] Quest refresh failed: {t.Exception.InnerException?.Message}");
            });
        }

        private static async Task RefreshAfterCompletion(LocalQuestControllerClass controller, Task completionTask)
        {
            // Wait for the original FinishQuest to complete
            await completionTask;

            // Small delay to let the server process the completion and update slots
            await Task.Delay(500);

            var questActions = controller.IQuestActions;
            if (questActions == null)
            {
                Plugin.Log.LogWarning("[MissionControl] IQuestActions is null, cannot refresh quest list");
                return;
            }

            // Re-request the quest list from server (which now has the updated slots)
            var templates = await questActions.RequestQuestsTemplates(true);
            if (templates == null || templates.Count == 0)
            {
                Plugin.Log.LogWarning("[MissionControl] Received empty quest list from server");
                return;
            }

            // Find the QuestBook via reflection on the base class (GClass4005)
            // The Quests property gives us the quest book
            var questsProperty = controller.GetType().BaseType?
                .GetProperty("Quests", BindingFlags.Public | BindingFlags.Instance);

            if (questsProperty?.GetValue(controller) is QuestBookClass questBook)
            {
                questBook.AddTemplates(templates);

                // Trigger OnConditionalStatusChanged to refresh trader badge icons
                // The event is stored as Action_0 in the base class GClass3999<QuestClass>
                var eventField = typeof(AbstractQuestControllerClass)
                    .GetField("Action_0", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (eventField?.GetValue(controller) is Action onStatusChanged)
                {
                    onStatusChanged.Invoke();
                    Plugin.Log.LogInfo($"[MissionControl] Quest list refreshed ({templates.Count} templates), trader badges updated");
                }
                else
                {
                    Plugin.Log.LogInfo($"[MissionControl] Quest list refreshed ({templates.Count} templates)");
                }
            }
            else
            {
                Plugin.Log.LogWarning("[MissionControl] Could not find QuestBook to refresh");
            }
        }
    }
}
