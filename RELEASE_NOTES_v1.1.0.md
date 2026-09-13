# MissionControl 1.1.0

## SPT 4.1 support

Runs on SPT 4.1.x (built against 4.1.5 and the .NET 10 server). Staying on 4.0.x? Keep using 1.0.1.

## Quests refresh live, no more reload

- Completing a quest refills the freed slot in the same server response: the new pick is in the Tasks screen immediately, and the sequels that were not picked stay hidden.
- Mission Reroll is consumed server-side the moment you buy it: the item never lands in your stash, your in-progress quests become exempt and the 3 fresh picks show up right away. No profile reload, no desync error on the item.
- The client plugin is retired, MissionControl is server-only now. If you still have `BepInEx/plugins/MissionControl.Client.dll` from 1.0.x, delete it.

## Config

Shipped defaults are now `filter_modded_quests: true` and `trader_whitelist: ["Quartermaster"]`, edit to taste. An existing config carries over as-is.

## Install

Extract the zip into your SPT root folder, it lands in `SPT_Runtime/user/mods/MissionControl`.
