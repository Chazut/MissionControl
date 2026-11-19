using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.Locales;
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

    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";

    private readonly TradersTable _traders;
    private readonly LocaleService _locales;
    private readonly CustomItemService _customItemService;
    private readonly ISptLogger<RerollService> _logger;

    public RerollService(TradersTable traders, LocaleService locales, CustomItemService customItemService, ISptLogger<RerollService> logger)
    {
        _traders = traders;
        _locales = locales;
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

    public void StockVendors(IReadOnlyList<string> vendors, int price)
    {
        if (vendors.Count == 0) return;
        var english = _locales.GetLocaleDb("en");
        var shops = _traders.Where(t => t.Key != Traders.FENCE && t.Value.Base?.AvailableInRaid != true
                                        && t.Value.Assort != null)
            .Select(t => (Id: t.Key.ToString(), Trader: t.Value,
                          Name: english.GetValueOrDefault($"{t.Key} Nickname") ?? t.Value.Base?.Nickname))
            .ToList();
        var stocked = new HashSet<string>();
        foreach (var vendor in vendors)
        {
            var name = vendor?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            var matches = shops.Where(shop => string.Equals(shop.Id, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(shop.Trader.Base?.Nickname, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(shop.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1)
            {
                _logger.Warning($"[MissionControl] Cannot resolve reroll vendor '{name}' to one shop. Available: "
                    + string.Join(", ", shops.Select(s => $"{s.Name} ({s.Id})")));
                continue;
            }

            var shop = matches[0];
            if (!stocked.Add(shop.Id)) continue;
            if (AddOffer(shop.Trader.Assort, price))
                _logger.Info($"[MissionControl] Reroll sold by {shop.Name} at LL1 for {price} RUB.");
            else
                _logger.Warning($"[MissionControl] Could not add the reroll to {shop.Name}'s shop.");
        }
    }

    private static bool AddOffer(TraderAssort assort, int price)
    {
        if (assort.Items is not List<Item> items ||
            assort.BarterScheme is not Dictionary<MongoId, List<List<BarterScheme>>> bs ||
            assort.LoyalLevelItems is not Dictionary<MongoId, int> lli)
            return false;

        var existing = items.FirstOrDefault(item => item.Template == RerollItemId && item.ParentId == "hideout");
        var offer = existing ?? new Item
        {
            Id = new MongoId(Guid.NewGuid().ToString("N")[..24]),
            Template = new MongoId(RerollItemId),
            ParentId = "hideout",
            SlotId = "hideout",
            Upd = new Upd
            {
                UnlimitedCount = true,
                StackObjectsCount = int.MaxValue
            }
        };
        if (existing == null) items.Add(offer);

        bs[offer.Id] = [
            [new BarterScheme { Template = new MongoId(RoublesTpl), Count = price }]
        ];
        lli[offer.Id] = 1;

        return true;
    }
}
