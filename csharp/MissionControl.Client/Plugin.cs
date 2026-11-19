using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;

namespace MissionControl.Client;

[BepInPlugin("com.chazut.missioncontrol.client", "MissionControl Client", "1.1.1")]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;
        new Harmony("com.chazut.missioncontrol.client").PatchAll();
    }
}

// EFT calls this for every successful trader purchase, even with wishlist notifications off.
[HarmonyPatch(typeof(TraderDealScreen), nameof(TraderDealScreen.TryShowWishlistPurchaseNotification))]
internal static class RerollPurchasePatch
{
    private static int latestPurchase;

    private static void Postfix(Item item, QuestController ____questController)
    {
        if (item?.TemplateId.ToString() != "a0b1c2d3e4f5000000000001" ||
            ____questController is not QuestControllerClientBackend controller)
            return;

        _ = RefreshAsync(controller, ++latestPurchase);
    }

    private static async Task RefreshAsync(QuestControllerClientBackend controller, int purchase)
    {
        try
        {
            var book = controller.Quests;
            var lifetime = book.compositeDisposable.CancellationToken;
            var templates = await controller.iQuestSession.RequestQuestsTemplates(withCompleted: true);
            if (lifetime.IsCancellationRequested || purchase != latestPurchase)
                return;
            if (templates == null)
                throw new InvalidOperationException("The quest list response was null.");

            var offeredIds = new HashSet<string>(templates.Select(template => template.Id));
            var global = ConditionalTemplatesStorage.Instance.GlobalQuestTemplates;
            int withdrawn = 0;
            foreach (var quest in book.ToArray())
            {
                if (quest.QuestStatus != EQuestStatus.AvailableForStart || quest.Template == null ||
                    quest.Template is RepeatableQuestTemplate || offeredIds.Contains(quest.Id) ||
                    !global.Contains(quest.Template))
                    continue;

                // Ordinary trader offers live in global templates. RemoveQuestTemplate targets daily quests.
                global.Remove(quest.Template);
                book.Remove(quest);
                quest.Dispose();
                withdrawn++;
            }

            book.AddTemplates(templates);
            // Trader portraits and the open Tasks list listen to OnConditionalStatusChanged.
            // Adding an already-available template does not raise that event.
            controller.action_0?.Invoke();
            Plugin.Log.LogInfo($"Reroll refreshed trader tasks ({withdrawn} withdrawn).");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not refresh trader tasks after the reroll: {ex.Message}");
        }
    }
}
