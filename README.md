# MissionControl

An SPT 4.1 mod that limits the number of quests visible at any time. Instead of being overwhelmed by dozens of available quests, you only see a handful — picked randomly from your unlocked quests.

## The Problem

With many mods installed, the quest list becomes unmanageable. You don't know what to prioritize, which weapon to bring, or which map to play. MissionControl fixes this by capping the number of visible quests, forcing you to focus.

## How It Works

- Only **N quests** are visible at once (default: 3), including quests already in progress
- Available quests are **randomly selected** from all your unlocked quests
- When you **complete** a quest, a new random one takes its slot immediately
- **Failed quests** stay in their slot — you can restart them from the UI
- Selections are **persisted** per profile across server restarts

### Example

With `max_slots: 3`:
- 0 quests in progress → 3 random available quests shown
- 1 quest in progress → 2 random available quests shown
- 3+ quests in progress → no new quests shown until you complete some

If installed on an existing profile with many active quests, no new quests will be proposed until you drop below the limit.

## Reroll

Buy the **"Mission Reroll"** item at **Prapor** (LL1). When purchased:

1. All your currently in-progress quests become **exempt** — they stay visible but no longer consume slots
2. **3 new random quests** are assigned to your slots
3. The new quests show up in the Tasks screen right away, no reload

This lets you break out of difficult quests without losing progress. Your stuck quests remain active alongside the new ones until you complete them.

## Installation

### Server mod

Copy `MissionControl.dll` and the `config/` folder to:
```
SPT_Runtime/user/mods/MissionControl/
├── MissionControl.dll
└── config/
    └── config.jsonc
```

### Client plugin: not needed anymore

Since 1.1.0 the server does the quest refresh itself. If you still have
`BepInEx/plugins/MissionControl.Client.dll` from 1.0.x, delete it.

## Configuration

Edit `config/config.jsonc`:

```jsonc
{
  // Maximum number of quests visible at once (in-progress + available combined).
  "max_slots": 3,

  // When true, modded quests are subject to the slot limit like vanilla quests.
  // When false, modded quests bypass the filter entirely (always visible).
  "filter_modded_quests": true,

  // Trader names whose quests bypass the slot filter entirely (always visible).
  // Case-insensitive. Works with both vanilla and modded traders.
  "trader_whitelist": ["Quartermaster"],

  // Cost in roubles for the reroll item at Prapor.
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
| `filter_modded_quests` | `true` | If `false`, modded quests are always visible and don't consume slots |
| `trader_whitelist` | `["Quartermaster"]` | Trader names (case-insensitive) whose quests bypass the filter |
| `reroll_cost` | `50000` | Rouble cost for the Mission Reroll item at Prapor |
| `debug` | `false` | Verbose server logging |
| `debug_refresh` | `false` | Clear saved selections on server start |

## Building from Source

Requires the `SPT_DIR` MSBuild property pointing to your SPT 4.1 runtime folder (`SPT_Runtime`), set in the csproj.

```bash
# Server mod
dotnet build csharp/MissionControl/MissionControl.csproj -c Release

```

## Compatibility

- SPT 4.1.x
- Works with all quest mods (modded quests are detected automatically)
- Works with custom traders

## License

MIT
