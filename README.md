# Kappa Tracker

SPT 4.1.2 mod - Track your Kappa container progress with a beautiful web UI.

## Features

- **Real-time progress tracking** — see your % towards Kappa
- **Per-trader breakdown** — missions completed, levels, requirements
- **Expandible quest details** — view all requirements for each quest
- **Auto-sync** — updates every 5 seconds without manual refresh
- **Persistent preferences** — remembers your selected trader

## Requirements

- SPT 4.1.2+
- .NET 10.0 SDK (for building)

## Installation

1. Extract the ZIP into your SPT root:
   ```
   SPT_Runtime/
     user/
       mods/
         KappaTracker/
   ```

2. Start SPT server
3. Open the SPT dashboard in your browser
4. Click "KappaTracker" in the mods list

## Usage

- Click a trader in the sidebar to view their quests
- Click "View Requirements" on a quest to see full details
- Global progress bar updates automatically every 5 seconds

## Config

`config.json` stores your UI preferences:
- `lastSelectedTraderId` — default trader on load
- `expandedQuestIds` — which quests are expanded
- `uiPrefs` — sidebar width, refresh interval, etc.

All changes are saved automatically.

## License

MIT
