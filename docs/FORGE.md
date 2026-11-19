## MissionControl {.tabset}

### Overview

Ever open the Tasks tab to pick your next raid and spend longer reading than playing? **MissionControl gives you three quest slots to start with.** It fills them with random picks from quests you've already unlocked. Finish one and another eligible quest takes its place.

With three slots and one quest already accepted, you'll see that quest plus up to two new offers. Your picks carry over between raids and server restarts. Story chapters, hidden steps that unlock traders, and the quests leading to Jaeger stay outside the limit and follow their usual requirements.

On a fresh install, each prestige from P1 to P6 adds one slot. If you'd rather keep three slots throughout or choose your own progression, open **MissionControl** in the SPT server web UI.

### Rerolls

Stuck on a quest you want to save for later? Buy the **Mission Reroll - Uplink** from **Prapor at LL1** for **50,000 ₽**. Croupier sells it at the same price if you have him installed.

The purchase refreshes the trader's Tasks tab straight away. Your accepted quests stay active and stop counting toward the limit. For example, if you have three accepted quests and three slots, a reroll lets you keep all three and see up to three fresh offers. The uplink is used when you buy it.

### Installation

1. Extract both folders from the ZIP into your SPT root, alongside `BepInEx` and `SPT_Runtime`. Allow the new files to replace older copies.
2. Start the server and game. If you're upgrading from the server-only 1.1.0 release, include the new client plugin so rerolls refresh the open trader screen.

| Component | Installed file |
|---|---|
| Client | `BepInEx/plugins/MissionControl.Client.dll` |
| Server | `SPT_Runtime/user/mods/MissionControl/MissionControl.dll` |

MissionControl creates its config on first launch. Upgrades keep your settings and saved selections. If your existing config has no prestige tiers, your base limit applies at every prestige until you add tiers in the editor.

### Settings

Open **MissionControl** in the SPT server web UI (`/missioncontrol`). You can also edit `SPT_Runtime/user/mods/MissionControl/config/config.jsonc` directly. Slot limits saved in the web UI apply on the next quest refresh. Restart the server for other settings or direct file edits.

| Setting | Fresh install | What it does |
|---|---|---|
| `max_slots` | `3` | Sets the base limit for accepted quests and new offers combined. |
| `max_slots_by_prestige` | P1 **4**, P2 **5**, P3 **6**, P4 **7**, P5 **8**, P6 **9** | Sets the limit at each prestige. A blank tier uses the previous limit. |
| `filter_modded_quests` | `true` | Includes modded quests in the selection. Turn it off to let unlocked modded quests bypass the limit. |
| `trader_whitelist` | `["Quartermaster"]` | Lets a trader's quests bypass the limit. Enter vanilla or custom trader names in any letter case. |
| `reroll_cost` | `50000` | Sets the uplink price in roubles at both traders. |
| `debug` | `false` | Writes detailed quest selection logs. |
| `debug_refresh` | `false` | Clears saved selections on every server start. Intended for testing. |

### Compatibility

- **SPT 4.1.x**, built against 4.1.6.
- **Existing profiles** keep their accepted quests. If those already fill your slots, completing one or buying a reroll makes room for fresh offers.
- **Story and custom traders** follow their normal unlock conditions. VisitAPI's Tour and its hidden progression steps bypass the limit. The BTR Driver quest line and Keeper's Word are excluded from random picks.
- To return to the usual quest list, remove the client plugin and the MissionControl server mod, then restart the server and game.

### Issues

If a quest seems to be missing, include its name, the trader and your PMC's prestige level when you [report the issue on GitHub](https://github.com/Chazut/MissionControl/issues).

### Support

All my mods are free and open source. If MissionControl helped you spend less time in menus and more time in raid, you can buy me a coffee.

[![Support Chazut on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/chazut)
