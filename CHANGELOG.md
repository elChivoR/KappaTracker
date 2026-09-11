# Changelog

## [1.3.0] - 2026-09-11

### Added
- Kappa-only toggle in the trader task filter bar
- Task Search mod integration — the `kappa` keyword now filters to Kappa-only quests when Task Search is installed

### Changed
- KAPPA badge moved to the status chip on both the task list and trader dialogue surfaces

## [1.2.0] - 2026-09-07

### Added
- Profile selector dropdown in the web UI — switch between saved profiles without needing the game launcher open
- Update checker — version badge and dismissible toast when a new release is available
- Active-session detection — the tracker automatically shows the profile of whoever is currently logged into SPT

## [1.1.0] - 2026-09-02

### Added
- In-game Kappa tags — BepInEx client plugin prefixes `KAPPA ·` on milestone quests in the task list, trader dialogue, and quest detail header
- Per-surface toggle and master switch via BepInEx config

## [1.0.1] - 2026-08-31

### Fixed
- Alt-quest routes (mutually exclusive quests) now correctly resolve as completed when the alternative was finished

## [1.0.0] - 2026-08-28

### Added
- Initial release — Blazor web UI at `/kappa` showing overall Kappa progress, per-trader breakdown, quest list with status, prerequisite chains, and partial objective tracking
