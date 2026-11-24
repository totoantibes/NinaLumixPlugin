# Does every marshalled struct in the plugin have the same size as Panasonic's?
# ============================================================================
# Compiles native_layout.cpp against the SDK headers in Library/ and compares each struct's
# real size with Marshal.SizeOf of the C# equivalent. A managed struct SMALLER than its native
# counterpart means the SDK writes past the end of what the plugin allocated - a corrupted heap
# that surfaces later, somewhere else, as an access violation in innocent code.
#
# This found LMX_STRUCT_LIVEVIEW_STR_RECOM_IMG at 12 managed bytes against 8196 native: its
# trailing RecomImgQualityListData[1024] had been declared as a single element.
#
# It also guards a standing hazard. LMX_lib_def.h is wrapped in "#pragma pack(push, Old, 1)",
# so every struct declared in it is byte-packed natively, while the C# structs declare no Pack
# at all. That the two currently agree is a property of these particular field layouts, not of
# the design: the next struct added with a member that needs alignment will diverge silently.
#
# Trailing padding is allowed to differ. LMX_STRUCT_SS_CAPA_INFO is 3106 native (packed, no tail
# padding) and 3108 managed (tail padded to 4), with every field offset identical, so the SDK
# writes less than the plugin allocated. That direction is safe; the reverse is not.
param(
    [string]$Dll     = (Join-Path $PSScriptRoot '..\bin\Release\net8.0-windows\LumixPlugin.dll'),
    [string]$Headers = (Join-Path $PSScriptRoot '..\Library'),
    [string]$Cpp     = (Join-Path $PSScriptRoot 'native_layout.cpp'),
    [string]$NinaDir = "C:\Program Files\N.I.N.A. - Nighttime Imaging 'N' Astronomy",
    [string]$VcVars  = 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat'
)
$ErrorActionPreference = 'Stop'

foreach ($p in @($Dll, $Headers, $Cpp)) {
    if (-not (Test-Path $p)) { Write-Host "ABORT: not found: $p" -ForegroundColor Red; exit 1 }
}
if (-not (Test-Path $VcVars)) {
    Write-Host "ABORT: no MSVC at $VcVars" -ForegroundColor Red
    Write-Host 'The native side has to come from Panasonic''s own headers, so this check needs a' -ForegroundColor Red
    Write-Host 'C++ compiler. Pass -VcVars if Visual Studio is installed elsewhere.' -ForegroundColor Red
    exit 1
}

# --- native: compile and run the layout probe --------------------------------------------
$work = Join-Path ([System.IO.Path]::GetTempPath()) ('lumix-layout-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work -Force | Out-Null
try {
    Copy-Item $Cpp (Join-Path $work 'native_layout.cpp') -Force
    $hdr = (Resolve-Path $Headers).Path
    $cmd = '"' + $VcVars + '" >nul 2>&1 && cd /d "' + $work + '"' +
           ' && cl /nologo /EHsc /I "' + $hdr + '" native_layout.cpp /Fe:native_layout.exe >build.log 2>&1' +
           ' && .\native_layout.exe'   # .\ required: cmd here does not search the current directory
    $nativeOut = cmd /c $cmd 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'ABORT: native probe failed to build or run' -ForegroundColor Red
        Get-Content (Join-Path $work 'build.log') -ErrorAction SilentlyContinue
        exit 1
    }

    $native = @{}
    foreach ($line in $nativeOut) {
        $f = ($line -split "`t")
        if ($f.Count -ge 3 -and $f[0] -eq 'SIZE') { $native[$f[1]] = [int]$f[2] }
    }
    ('native structs measured : ' + $native.Count)
} finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}

# --- managed: Marshal.SizeOf of each nested value type -----------------------------------
$resolver = {
    param($sender, $e)
    $n = ($e.Name -split ',')[0]
    $c = Join-Path $NinaDir ($n + '.dll')
    if (Test-Path $c) { return [System.Reflection.Assembly]::LoadFrom($c) }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
$asm = [System.Reflection.Assembly]::LoadFrom((Resolve-Path $Dll).Path)
$t = $asm.GetType('LumixWrapper.LumixCam', $true)

# PowerShell binds SizeOf(object) when handed a RuntimeType, which measures the Type object
# itself and throws. Pick the SizeOf(Type) overload explicitly.
$sizeOf = [System.Runtime.InteropServices.Marshal].GetMethod('SizeOf', [Type[]]@([Type]))
if (-not $sizeOf) { Write-Host 'ABORT: no SizeOf(Type) overload' -ForegroundColor Red; exit 1 }

$managed = @{}
foreach ($nt in $t.GetNestedTypes([System.Reflection.BindingFlags]'Public,NonPublic')) {
    if (-not $nt.IsValueType -or $nt.IsEnum) { continue }
    try { $managed[$nt.Name] = [int]$sizeOf.Invoke($null, @([Type]$nt)) } catch { }
}
('managed structs measured: ' + $managed.Count)
''

# --- compare ------------------------------------------------------------------------------
# The C# names differ in case for a few (UINT32 -> UInt32), so match case-insensitively, plus a
# small alias table where the C# name is not a case variant of the native one.
$alias = @{ 'LMX_DEV_INFO' = 'LMX_DEVINFO' }
$bad = @()
$pad = @()
$unmatched = @()
$ok = 0

foreach ($name in ($native.Keys | Sort-Object)) {
    $want = if ($alias.ContainsKey($name)) { $alias[$name] } else { $name }
    $m = $managed.Keys | Where-Object { $_ -ieq $want } | Select-Object -First 1
    if (-not $m) { $unmatched += $name; continue }
    $n = $native[$name]; $g = $managed[$m]
    if ($g -eq $n) { $ok++ }
    elseif ($g -gt $n) { $pad += ('{0,-44} native {1,7}  managed {2,7}  (+{3}, trailing padding)' -f $name, $n, $g, ($g - $n)) }
    else { $bad += ('{0,-44} native {1,7}  managed {2,7}  (SHORT BY {3})' -f $name, $n, $g, ($n - $g)) }
}

('exact matches                  : ' + $ok)
('managed larger (safe padding)  : ' + $pad.Count)
('managed SHORTER (overflow)     : ' + $bad.Count)
('native struct with no C# type  : ' + $unmatched.Count)
''
if ($pad.Count)       { 'managed larger - the SDK writes less than was allocated, safe:'; $pad | ForEach-Object { '  ' + $_ }; '' }
if ($unmatched.Count) { 'declared natively but not marshalled by the plugin (not a defect):'; $unmatched | ForEach-Object { '  ' + $_ }; '' }
if ($bad.Count) {
    Write-Host 'MANAGED STRUCT SHORTER THAN NATIVE - the SDK will write past it:' -ForegroundColor Red
    $bad | ForEach-Object { Write-Host ('  ' + $_) -ForegroundColor Red }
    ''
    'FAILED: fix the C# declaration (usually a missing MarshalAs ByValArray SizeConst) before'
    'anything calls a function that takes one of these by ref.'
    exit 1
}
'CONFIRMED: no marshalled struct is smaller than the native struct the SDK writes into.'
exit 0
