# Does every P/Invoke in the plugin name a symbol that actually exists in Panasonic's DLL?
# ======================================================================================
# This check exists because it was missing. The gate commit renamed eight externs to Raw_*,
# those DllImports use ExactSpelling = true with NO EntryPoint, and in that case the native
# symbol name defaults to the MANAGED method name - so the rename renamed the symbol the
# runtime looks for. Neither native DLL has Raw_* exports, NINA threw
# EntryPointNotFoundException at Init, and the camera came up offline. One deploy and one
# wasted camera session, for something fully decidable here with no device attached.
#
# Two candidate DLLs, chosen at runtime by NativeBinding.Resolve:
#   public  - the bundled SDK DLL in the plugin folder            (Standard mode)
#   tether  - C:\Program Files\Panasonic\LUMIX Tether\Lmxptpif.dll (Extended mode)
# They export DIFFERENT name sets. The Ext_* bindings exist only in the Tether DLL, and five
# of the shared underscore names (Open_Session, Close_Session, Close_Device, Get_PnPDeviceInfo,
# Select_PnPDevice) only in the public one - they are simply not called in extended mode.
# So the rule here is: every effective entry point must exist in AT LEAST ONE of the two.
# A name in neither cannot resolve in either mode and will throw on first call.
#
# Effective entry point = EntryPoint if declared, else the managed method name. With
# ExactSpelling = $true that name must match an export exactly; with $false the runtime may
# also probe name+"A"/name+"W", so those are accepted and the row says so.
param(
    [string]$Dll        = 'C:\Users\rober\source\repos\LumixPlugin\bin\Release\net8.0-windows\LumixPlugin.dll',
    [string]$PublicDll  = 'C:\Users\rober\source\repos\LumixPlugin\Library\Lmxptpif.dll',
    [string]$TetherDll  = 'C:\Program Files\Panasonic\LUMIX Tether\Lmxptpif.dll',
    [string]$PeExports  = (Join-Path $PSScriptRoot 'pe_exports.py'),
    [string]$NinaDir    = "C:\Program Files\N.I.N.A. - Nighttime Imaging 'N' Astronomy"
)
$ErrorActionPreference = 'Stop'

foreach ($p in @($Dll, $PublicDll, $TetherDll, $PeExports)) {
    if (-not (Test-Path $p)) { Write-Host "ABORT: not found: $p" -ForegroundColor Red; exit 1 }
}

function Get-Exports([string]$path) {
    $names = & python $PeExports $path
    if ($LASTEXITCODE -ne 0) { Write-Host "ABORT: pe_exports.py failed on $path" -ForegroundColor Red; exit 1 }
    return [System.Collections.Generic.HashSet[string]]::new([string[]]$names, [System.StringComparer]::Ordinal)
}

$pub = Get-Exports $PublicDll
$tet = Get-Exports $TetherDll
('public DLL : ' + (Get-Item $PublicDll).Length + ' bytes, ' + $pub.Count + ' named exports   ' + $PublicDll)
('tether DLL : ' + (Get-Item $TetherDll).Length + ' bytes, ' + $tet.Count + ' named exports   ' + $TetherDll)

# --- managed side: every DllImport and its effective entry point ------------------------
$resolver = {
    param($sender, $e)
    $n = ($e.Name -split ',')[0]
    $c = Join-Path $NinaDir ($n + '.dll')
    if (Test-Path $c) { return [System.Reflection.Assembly]::LoadFrom($c) }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

$cs = @'
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

public class EntryPoints {
    // "lib|declaringType.method|effectiveEntryPoint|exact|probe"
    public static List<string> Scan(Assembly asm) {
        List<string> rows = new List<string>();
        BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (Type t in asm.GetTypes()) {
            MethodInfo[] ms;
            try { ms = t.GetMethods(f); } catch (Exception) { continue; }
            foreach (MethodInfo mi in ms) {
                if ((mi.Attributes & MethodAttributes.PinvokeImpl) == 0) { continue; }
                DllImportAttribute di = null;
                try {
                    object[] a = mi.GetCustomAttributes(typeof(DllImportAttribute), false);
                    if (a.Length > 0) { di = (DllImportAttribute)a[0]; }
                } catch (Exception) { }
                if (di == null) { continue; }
                // GetCustomAttributes rebuilds this pseudo-attribute from metadata, so an
                // empty EntryPoint means "default to the managed method name" - the trap.
                string ep = string.IsNullOrEmpty(di.EntryPoint) ? mi.Name : di.EntryPoint;
                rows.Add((di.Value == null ? "(null)" : di.Value) + "|" +
                         t.Name + "." + mi.Name + "|" + ep + "|" +
                         (di.ExactSpelling ? "exact" : "probe"));
            }
        }
        return rows;
    }
}
'@
Add-Type -TypeDefinition $cs -Language CSharp

$asm = [System.Reflection.Assembly]::LoadFrom($Dll)
('assembly   : ' + (Get-Item $Dll).Length + ' bytes   ' + $Dll)
''

function Test-Name($set, [string]$ep, [string]$spell) {
    if ($set.Contains($ep)) { return 'exact' }
    if ($spell -eq 'probe') {
        foreach ($sfx in @('A', 'W')) { if ($set.Contains($ep + $sfx)) { return $sfx } }
    }
    return $null
}

$rows    = [EntryPoints]::Scan($asm) | Sort-Object
$missing = @()
$other   = @()
$both = 0; $pubOnly = @(); $tetOnly = @()

foreach ($row in $rows) {
    $p = $row -split '\|'
    $lib = $p[0]; $method = $p[1]; $ep = $p[2]; $spell = $p[3]
    if ($lib -notmatch 'Lmxptpif') { $other += ($lib + '  ' + $method); continue }

    $inPub = Test-Name $pub $ep $spell
    $inTet = Test-Name $tet $ep $spell

    if ($inPub -and $inTet)  { $both++ }
    elseif ($inPub)          { $pubOnly += ('{0,-46} -> {1}' -f $method, $ep) }
    elseif ($inTet)          { $tetOnly += ('{0,-46} -> {1}' -f $method, $ep) }
    else {
        $missing += ('{0,-46} -> {1}   ({2} spelling)' -f $method, $ep, $spell)
    }
}

$checked = $both + $pubOnly.Count + $tetOnly.Count + $missing.Count
('P/Invokes into Lmxptpif.dll : ' + $checked)
('  exported by BOTH DLLs     : ' + $both)
('  public DLL only           : ' + $pubOnly.Count + '   (not called in Extended mode)')
('  Tether DLL only           : ' + $tetOnly.Count + '   (the Ext_* extended bindings)')
('  exported by NEITHER       : ' + $missing.Count)
''
if ($pubOnly.Count) { '--- public-only (fine, mode-specific) ---'; $pubOnly | ForEach-Object { '  ' + $_ } ; '' }

if ($missing.Count) {
    Write-Host '--- EXPORTED BY NEITHER DLL ---' -ForegroundColor Red
    $missing | ForEach-Object { Write-Host ('  ' + $_) -ForegroundColor Red }
    ''
}
if ($other.Count) {
    ('P/Invokes into other DLLs (not checked here) : ' + $other.Count)
    $other | Select-Object -Unique | ForEach-Object { '    ' + $_ }
    ''
}

if ($missing.Count -eq 0) {
    'CONFIRMED: every P/Invoke into Lmxptpif.dll names a symbol one of the two DLLs exports.'
    exit 0
} else {
    'FAILED: the names above exist in neither DLL, so they cannot resolve in either mode and'
    'will throw EntryPointNotFoundException on first call. Add EntryPoint = "<native name>".'
    exit 1
}
