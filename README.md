<img src="branding/mission-control.svg" width="88" alt="MissionControl uplink" />

# MissionControl

Plan your next SPT raid around a handful of quests. MissionControl starts you with **3 quest slots** and randomly fills them from the quests you've unlocked. Your selection carries over between raids and server restarts.

## Quick start

1. Extract both folders from the release ZIP into your **SPT 4.1.x** game folder, next to `EscapeFromTarkov.exe`. For a shared server, install the server mod on the host and the client plugin on each playing client.
2. Start the server and game. Open a trader's **Tasks** tab to see your quests.
3. To change the defaults, open **MissionControl** in the SPT server web UI (`/missioncontrol`).

## How quest slots work

With 3 slots and 1 quest in progress, you can see up to 2 new offers. Finish a quest and another unlocked one fills its slot. Existing profiles keep every accepted quest, even if the list is already over the limit. A failed quest that you can restart keeps its slot.

- **Prestige** adds one slot per level by default, from 4 at P1 to 9 at P6.
- **Vanilla and modded quests** share the limit. Daily and weekly tasks are extra by default.
- **Story chapters and trader unlocks** follow their usual requirements. This includes the quests that unlock Jaeger and VisitAPI's Tour. BTR Driver quests and Keeper's Word are excluded from random picks.

## Reroll your offers

Buy the **Mission Reroll - Uplink** from **Prapor at loyalty level 1** for **50,000 ₽**. The purchase refreshes the open Tasks tab immediately. If all 3 slots are tied up in quests you'd like to save for later, you keep all 3 and can get up to 3 fresh offers.

## Web settings

In the web UI you can set the number of slots at each prestige, choose whether modded quests or accepted daily and weekly tasks use a slot, and pick traders whose quests always stay visible. You can also change who sells the uplink and its price.

Slot limits and repeatable task counting apply on the next quest refresh. Restart the server for other settings to take effect.

## Build from source

Use the .NET 10 SDK and an SPT 4.1.x installation. From the repository root, point the server build at the `SPT_Runtime` folder and the client build at the game folder. Adjust the paths for your installation.

```powershell
dotnet build csharp/MissionControl/MissionControl.csproj -c Release -p:SPT_DIR="C:\Games\SPT-4.1\SPT_Runtime"
dotnet build csharp/MissionControl.Client/MissionControl.Client.csproj -c Release -p:GameRoot="C:\Games\SPT-4.1"
```

Thanks to pngv for suggesting prestige-based quest limits.

[Release notes for 1.2.0](RELEASE_NOTES_v1.2.0.md) · [Report an issue](https://github.com/Chazut/MissionControl/issues) · [MIT license](LICENSE)
