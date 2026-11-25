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
| `lumix-race-probe.ps1` | **Needs the camera.** Is the Tether DLL thread-safe on one context? |
| `lumix-late-write-probe.ps1` | **Needs the camera, fires the shutter.** Does the SDK finish an abandoned transfer? |

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

## verify-struct-sizes.ps1

Compiles `native_layout.cpp` against the SDK headers in `Library/` with MSVC and compares every
struct's real size with `Marshal.SizeOf` of the C# equivalent. A managed struct smaller than its
native counterpart means the SDK writes past what the plugin allocated.

It found `LMX_STRUCT_LIVEVIEW_STR_RECOM_IMG` at 12 managed bytes against 8196 native. It also
guards a standing hazard: `LMX_lib_def.h` is wrapped in `#pragma pack(push, Old, 1)` while the C#
structs declare no `Pack`, so their agreement is a property of the current field layouts, not of
the design.

## lumix-getobject-guard.ps1 (needs the camera)

The one hardware probe here. Runs on the imaging PC under Windows PowerShell 5.1 with NINA
closed, fires one frame, and checks on guarded native buffers whether `Get_Object_DataSize`
writes 8 bytes (it does: `UINT64*`) and whether `Get_Object` stays inside `storeBufSize`.

## The two hardware probes

Both run on the imaging PC under Windows PowerShell 5.1 with NINA closed. Between them they
reproduce the fault described in `docs/crash-analysis-2026-11-24.md` on demand, which is why
that document no longer has to rely on the two crash dumps.

`lumix-race-probe.ps1 -Phase 2,3` then `-Phase 4` makes the case for the gate: 2,000 calls on
one thread are clean, the same calls on two threads with no lock deadlock the DLL within
seconds, and two threads with one lock are clean again. Reads only - no shutter.

`lumix-late-write-probe.ps1` goes further and needs a frame: it collides one battery read with a
real 18 MB `Get_Object`, which makes the fetch return 0 having written nothing, abandons the
transfer as the plugin does, and then watches. Run under page heap it caught the SDK writing to
freed memory - `AutoVerifierV2`, `VCRUNTIME140.dll`, `c0000005`. It writes one camera setting,
the save target, because without it no PC transfer is ever offered.

Read the "Prerequisites" note in the crash-analysis document before changing either: several of
the SDK's constants and signatures are not what you would guess, and getting one wrong wastes a
run or crashes the probe before it does any work.
