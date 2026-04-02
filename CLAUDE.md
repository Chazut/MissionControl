# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

An SPT (Single Player Tarkov) 4.0 server mod that limits the number of quests visible to the player at any time. Instead of seeing all available quests, the player sees only N quests (configurable, default 3), picked randomly from all available ones. Quests already in progress count toward the limit.

## Build Commands

```bash
# Requires SPT_DIR environment variable pointing to SPT 4.0 server root
# e.g., export SPT_DIR="C:/Games/SPT-4.0/SPT"

dotnet build csharp/MissionControl/MissionControl.csproj -c Release
```

## Architecture

**Framework:** .NET 9.0 targeting SPT 4.0's DI system. Classes use `[Injectable]` for auto-discovery.

**Core mechanism:**
1. `QuestListRouter` — `StaticRouter` intercepting `/client/quest/list` responses. Filters the quest list to only show N quests total (in-progress + randomly selected available).
2. `SlotManager` — Manages quest slot allocation per profile. Tracks which available quests are currently "selected" for display.
3. `ProfileStateStorage` — Persists slot selections to JSON files per profile.
4. `QuestEventRouter` — Intercepts quest completion/failure events. Auto-restarts failed quests (keeps the slot).

**Rules:**
- Total visible quests (Started + AvailableForFinish + selected AvailableForStart) capped at N
- If player already has N+ quests in progress → no new quests shown
- Rotation only on quest completion (not failure — failed quests auto-restart)
- Slot selections persist across server restarts

## Code Style

- `.editorconfig`: UTF-8, CRLF, 4-space indentation for C#
- Nullable reference types enabled, implicit usings enabled
- No test suite
