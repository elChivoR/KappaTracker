# Client plugin reference assemblies

These DLLs are **not redistributable** (they belong to Escape From Tarkov / BepInEx),
so they are git-ignored. Drop them here to build the in-game plugin, or set the
`EFT_MANAGED` environment variable to a folder that contains them.

From your SPT install:

| File | Source folder |
|---|---|
| `Assembly-CSharp.dll` | `EscapeFromTarkov_Data/Managed/` |
| `Newtonsoft.Json.dll` | `EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.dll` | `EscapeFromTarkov_Data/Managed/` |
| `UnityEngine.CoreModule.dll` | `EscapeFromTarkov_Data/Managed/` |
| `Unity.TextMeshPro.dll` | `EscapeFromTarkov_Data/Managed/` |
| `BepInEx.dll` | `BepInEx/core/` |
| `0Harmony.dll` | `BepInEx/core/` |
| `spt-common.dll` | `BepInEx/plugins/spt/` |

The build only compiles the plugin when `BepInEx.dll` is present here (or
`EFT_MANAGED` is set); otherwise it builds the server mod alone.
