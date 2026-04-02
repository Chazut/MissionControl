# MissionControl

An SPT 4.0 mod that limits the number of quests visible at any time. Instead of being overwhelmed by dozens of available quests, you only see a handful — picked randomly from your unlocked quests.

## The Problem

With many mods installed, the quest list becomes unmanageable. You don't know what to prioritize, which weapon to bring, or which map to play. MissionControl fixes this by capping the number of visible quests, forcing you to focus.

## How It Works

- Only **N quests** are visible at once (default: 3), including quests already in progress
- Available quests are **randomly selected** from all your unlocked quests
- When you **complete** a quest, a new random one takes its slot immediately
- **Failed quests** stay in their slot — you can restart them from the UI
- Selections are **persisted** per profile across server restarts
- **Reroll** your quest slots at any time via in-game chat (costs roubles)

### Example

With `max_slots: 3`:
- 0 quests in progress → 3 random available quests shown
- 1 quest in progress → 2 random available quests shown
- 3+ quests in progress → no new quests shown until you complete some

If installed on an existing profile with many active quests, no new quests will be proposed until you drop below the limit.

## Reroll

Open the in-game messenger, find **"Mission Control"**, and type:

```
mc reroll
```

This clears all your current quest slots (including accepted but unfinished quests) and picks new random ones. Costs roubles (configurable, default 50,000₽).

## Installation

### Server mod

Copy `MissionControl.dll` and the `config/` folder to:
```
SPT/user/mods/MissionControl/
├── MissionControl.dll
└── config/
    └── config.jsonc
```

### Client plugin (required)

Copy `MissionControl.Client.dll` to:
```
BepInEx/plugins/MissionControl.Client.dll
```

This BepInEx plugin ensures the quest list and trader badges refresh immediately after completing a quest.

## Configuration

Edit `config/config.jsonc`:

```jsonc
{
  // Maximum number of quests visible at once (in-progress + available combined).
  "max_slots": 3,

  // When true, modded quests are subject to the slot limit like vanilla quests.
  // When false, modded quests bypass the filter entirely (always visible).
  "filter_modded_quests": false,

  // Trader names whose quests bypass the slot filter entirely (always visible).
  // Case-insensitive. Works with both vanilla and modded traders.
  // If a name is not found, available traders will be listed in the server log.
  "trader_whitelist": ["Kolya", "Guiding Light"],

  // Cost in roubles to reroll quest slots via "mc reroll" chat command.
  // Set to 0 for free rerolls.
  "reroll_cost": 50000,

  // Enable verbose logging for debugging.
  "debug": false,

  // Clears all saved slot selections on server start. Development only.
  "debug_refresh": false
}
```

### Options

| Option | Default | Description |
|---|---|---|
| `max_slots` | `3` | Total quest slots (in-progress + available) |
| `filter_modded_quests` | `false` | If `false`, modded quests are always visible and don't consume slots |
| `trader_whitelist` | `[]` | Trader names (case-insensitive) whose quests bypass the filter |
| `reroll_cost` | `50000` | Rouble cost for `mc reroll` chat command (0 = free) |
| `debug` | `false` | Verbose server logging |
| `debug_refresh` | `false` | Clear saved selections on server start |

## Building from Source

Requires the `SPT_DIR` environment variable pointing to your SPT 4.0 server root.

```bash
# Server mod
dotnet build csharp/MissionControl/MissionControl.csproj -c Release

# Client plugin (requires dependencies/ folder with BepInEx + Assembly-CSharp DLLs)
dotnet build csharp/MissionControl.Client/MissionControl.Client.csproj -c Release
```

## Compatibility

- SPT 4.0.x
- Works with all quest mods (modded quests are detected automatically)
- Works with custom traders

## License

MIT
