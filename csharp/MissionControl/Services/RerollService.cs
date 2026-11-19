using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace MissionControl.Services;

[Injectable]
public sealed class RerollService
{
    public const string RerollItemId = "a0b1c2d3e4f5000000000001";

    private const string UltralinkSatelliteStation = "6389c88b33a719183c7f63b6";
    private const string ParentClass = "5447e0e74bdc2d3c308b4567";
    private const string ElectronicsCategory = "5b47574386f77428ca22b2ef";

    private const string PraporId = "54cb50c76803fa8b248b4571";
    private const string CroupierId = "1337bb0dd843363fcd1be869";
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";

    private readonly TradersTable _traders;
    private readonly CustomItemService _customItemService;
    private readonly ISptLogger<RerollService> _logger;

    public RerollService(TradersTable traders, CustomItemService customItemService, ISptLogger<RerollService> logger)
    {
        _traders = traders;
        _customItemService = customItemService;
        _logger = logger;
    }

    public bool CreateRerollItem()
    {
        // Exact same pattern as TTC CardItemFactory
        // 4.1: NewItemName is required; AddToFleaPriceDb must be false when no
        // flea price is provided (throws otherwise) and also blacklists the
        // item from PMC loot as a side effect, which is what we want here.
        var details = new NewItemFromCloneDetails
        {
            NewId = RerollItemId,
            ItemTplToClone = UltralinkSatelliteStation,
            ParentId = ParentClass,
            NewItemName = "item_missioncontrol_reroll",
            HandbookParentId = ElectronicsCategory,
            HandbookPriceRoubles = 50000,
            FleaPriceRoubles = null,
            AddToFleaPriceDb = false,
            AddToWeaponShelf = false,
            OverrideProperties = new TemplateItemProperties
            {
                BackgroundColor = "blue",
                Width = 1,
                Height = 1,
                StackMaxSize = 1,
                Weight = 0,
                DiscardLimit = 1,
                ExamineExperience = 10,
                LootExperience = 40,
                RarityPvE = "Common",
                ExaminedByDefault = true,
                CanSellOnRagfair = false,
                CanRequireOnRagfair = false
            },
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = "Mission Reroll - Uplink",
                    ShortName = "Reroll",
                    Description = "Transmit a new task request to Mission Control. Buying this uplink refreshes the missions on offer. Your accepted missions stay active."
                },
                ["fr"] = new LocaleDetails
                {
                    Name = "Relance de missions - Liaison orbitale",
                    ShortName = "Relance",
                    Description = "Transmettez une nouvelle demande à Mission Control. L'achat de cette liaison renouvelle les missions proposées. Vos missions acceptées restent actives."
                }
            }
        };

        var result = _customItemService.CreateItemFromClone(details);
        if (!result.Success)
        {
            _logger.Warning($"[MissionControl] Failed to create reroll item: {string.Join(", ", result.Errors)}");
            return false;
        }
        return true;
    }

    public bool AddToPrapor(int price) => AddToTrader(PraporId, price);

    public bool AddToCroupier(int price) => AddToTrader(CroupierId, price);

    private bool AddToTrader(string traderId, int price)
    {
        if (!_traders.TryGetValue(traderId, out var trader) || trader?.Assort == null)
            return false;

        var assort = trader.Assort;
        if (assort.Items is not List<Item> items ||
            assort.BarterScheme is not Dictionary<MongoId, List<List<BarterScheme>>> bs ||
            assort.LoyalLevelItems is not Dictionary<MongoId, int> lli)
            return false;

        var assortId = new MongoId(Guid.NewGuid().ToString("N")[..24]);
        items.Add(new Item
        {
            Id = assortId,
            Template = new MongoId(RerollItemId),
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = true,
                StackObjectsCount = int.MaxValue
            }
        });

        bs[assortId] = [
            [new BarterScheme { Template = new MongoId(RoublesTpl), Count = price }]
        ];
        lli[assortId] = 1;

        return true;
    }
}
