# Static checks on the built plugin

Four scripts that inspect `bin/Release/net8.0-windows/LumixPlugin.dll` after a build. None of
them needs a camera, NINA, or the imaging PC, and each answers a question that otherwise costs
a deploy and a camera session to get wrong.

Run them from the repo root with PowerShell 7 (`pwsh`), which is required because the plugin
targets `net8.0` and Windows PowerShell 5.1 cannot load it.

| script | question it answers |
| --- | --- |
| `verify-entrypoints.ps1` | Does every `DllImport` name a symbol one of the two native DLLs actually exports? |
| `verify-no-ungated-path.ps1` | Does any method reach `Lmxptpif.dll` without taking the SDK gate? |
| `verify-gate-field.ps1` | Do all the gated entry points lock on the *same* field? |
| `pe_exports.py` | Helper: prints a PE DLL's exported names (used by the first script). |

## verify-entrypoints.ps1

This exists because it was missing. The commit that serialised the native calls renamed eight
externs to `Raw_*`. Those `DllImport`s use `ExactSpelling = true` with no `EntryPoint`, and in
that case the native symbol name **defaults to the managed method name** — so the rename
renamed the symbol the runtime looks for. Neither native DLL exports anything called `Raw_*`,
NINA threw `EntryPointNotFoundException` at `LMX_func_api_Init`, `GetEquipment` reported
`Found 0 Lumix Cameras`, and the camera came up offline. The whole thing was decidable from the
two DLL export tables.

The plugin binds to one of two native DLLs, chosen at runtime by `NativeBinding.Resolve`:

- **public** — the SDK DLL bundled in `Library/` (Standard mode)
- **tether** — `C:\Program Files\Panasonic\LUMIX Tether\Lmxptpif.dll` (Extended mode)

Their export sets differ. The `Ext_*` bindings exist only in the Tether DLL; five shared
underscore names (`Open_Session`, `Close_Session`, `Close_Device`, `Get_PnPDeviceInfo`,
`Select_PnPDevice`) only in the public one, because extended mode does not call them. So the
rule is that every effective entry point must exist in **at least one** of the two. A name in
neither cannot resolve in either mode.

Panasonic's Tether DLL is not redistributable and is not in this repo. On a machine without
LUMIX Tether installed, point the script at a copy:

```
pwsh -NoProfile -File tools/verify-entrypoints.ps1 -TetherDll <path-to-a-copy>
```

The imaging PC has it at `C:\Program Files\Panasonic\LUMIX Tether\Lmxptpif.dll`.
