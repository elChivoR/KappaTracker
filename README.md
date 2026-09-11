# Kappa Tracker

> A SPT server mod that adds a `/kappa` dashboard to the SPT web UI for tracking your
> progress toward the **Kappa** secure container — every milestone quest, its
> requirements, and the full chain of quests you still need to unlock it — plus a
> companion BepInEx plugin that tags those quests inside the game's own task UI.

![Version](https://img.shields.io/badge/version-1.3.0-e6a23c)
![SPT](https://img.shields.io/badge/SPT-4.1.x-blue)
![License](https://img.shields.io/badge/license-MIT-green)

![Kappa Tracker dashboard](docs/media/dashboard.png)

---

## Features

- **Overall Kappa progress** — a single live percentage over all **137** milestone
  quests (the *Collector* quest plus every quest it requires).
- **Per-trader breakdown** — loyalty level, quests completed / total and a progress
  bar for each trader that has Kappa quests.
- **Character level** shown next to your profile name.
- **Expandable quest panels** — for every Kappa quest of the selected trader:
  - live status: `Locked` · `Available` · `In progress` · `Turn in` · `Completed` · `Failed`
  - hand-in requirements with **item icons**, the amount you currently own, the
    amount required and whether it must be **Found in Raid**.
- **Path to unlock** — the full transitive chain of prerequisite quests for each
  milestone, in the order you can complete them, with:
  - gate notes (`Lv 12`, `LL 2 Prapor`, …) showing what unlocks each step
  - a `Kappa` tag on steps that are themselves milestone quests
  - per-step status and a completed / total counter
- **Quest-detail modal** — click any step in *Path to unlock* to open a modal with
  that quest's description, current status, unlock gate and hand-in requirements.
- **Auto-sync** — the whole dashboard refreshes every 5 seconds; no manual reload.
- **No footprint** — item icons are fetched once from `assets.tarkov.dev` and kept
  **in memory only**; nothing is written into your mod folder. (The in-game plugin's
  only file is its BepInEx config.)
- **In-game Kappa tags** — a companion BepInEx plugin prefixes `KAPPA · ` to every
  milestone quest in the game's own task UI: the Tasks screen, the trader task list
  and the quest detail header. Each surface is toggled individually in
  `BepInEx/config/com.elchivor.kappatracker.client.cfg`. (An in-raid HUD toggle is
  present but off by default — EFT has no separate in-raid task tracker to hook, and
  the in-raid Tasks screen is the same list that's already tagged.)

## Browsing quest details
https://github.com/user-attachments/assets/9cceeba7-cc8a-4027-bb74-b5107725b977

---

## Installation

1. Download `KappaTracker.zip` from the [latest release](https://github.com/elChivoR/KappaTracker/releases/latest).
2. Extract it into your **SPT root** (the folder that contains `SPT_Runtime/`). The
   archive already has the right layout, with `BepInEx/` a sibling of `SPT_Runtime/`:
   ```
   SPT_Runtime/
     user/mods/KappaTracker/
       KappaTracker.dll
       wwwroot/
   BepInEx/
     plugins/KappaTracker/
       KappaTracker.Client.dll
   ```
   `KappaTracker.Client.dll` is the in-game tag plugin. It reads the milestone list
   from the running server mod over `/kappa/api/milestones`; with the server stopped
   it simply does nothing.
3. Start the SPT server.
4. Open the SPT web dashboard and pick **Kappa Tracker** from the mod list
   (or go straight to `/kappa`).

**Requirements:** SPT `4.1.x`. The tag plugin runs on BepInEx (already required by
SPT) and needs the server mod running to know which quests are milestones. An
internet connection is used only to fetch item icons; the mod works offline without
them.

---

## How it works

The mod reads the live quest database and your active profile on the server side:

- The milestone set is the *Collector* quest (`5c51aac1…`) plus every quest listed
  in its `AvailableForStart` conditions — 137 quests total. The overall percentage
  is `completed / 137`.
- `QuestGraphService` builds a directed graph of quest prerequisites from the quest
  DB on first use and walks it to produce each milestone's *Path to unlock*,
  attaching the level / loyalty gate that unlocks every step.
- Everything is read-only. The mod never changes your profile, quest state or the
  Kappa requirements.

---

## Building from source

**Requires:** .NET 10 SDK.

Build the **server project**, not the solution — the `.sln` also holds the in-game
client plugin and its tests, which only build with an SPT install on hand (below):

```bash
dotnet build KappaTracker.csproj -c Release
```

On a plain clone this builds and zips the server mod alone: the build prints a
`[KappaTracker] client plugin skipped` notice and `bin/Release/KappaTracker.zip`
contains only `SPT_Runtime/`.

### Also building the in-game plugin

The `KappaTracker.Client` plugin targets .NET Framework 4.7.2 and references EFT / SPT
game assemblies **in place** — nothing is copied into the repo. BepInEx and HarmonyX
come from the [BepInEx NuGet feed](https://nuget.bepinex.dev) (see `NuGet.Config`).

Point the build at your SPT install root (the folder with `EscapeFromTarkov_Data/` and
`BepInEx/`) one of two ways:

- set the `SPT_ROOT` environment variable, or
- copy [`client/KappaTracker.Client.props.user.example`](client/KappaTracker.Client.props.user.example)
  to `client/KappaTracker.Client.props.user` (git-ignored) and set `<SptRoot>`

then `dotnet build KappaTracker.csproj -c Release` also adds
`BepInEx/plugins/KappaTracker/` to the zip. `dotnet build KappaTracker.sln` additionally
builds the xUnit tests for `KappaTagService`.

---

## License

MIT — see [LICENSE](LICENSE).
