# Lumix SDK race / buffer-overflow probe
# =========================================================================================
# RUN ON THE IMAGING PC (192.168.1.185), WITH NINA CLOSED AND THE GH5S CONNECTED AND ON.
#
# What it tests, and why
# ----------------------
# The plugin holds ONE pinned 8 KB buffer as the Panasonic SDK's session context
# (Library/LumixCam.Tether.cs: _ctxBacking = new byte[8192]) and passes it to 38 native
# entry points, including both the image fetch (LMX_func_api_Get_Object) and the battery
# read (LMX_func_api_GetBatteryInfo). There is no lock, Monitor, Mutex or Interlocked
# anywhere in that file. Two separate hypotheses follow:
#
#   H1 (overflow)  8192 is a guess at an undocumented native struct. If the SDK writes
#                  past it, in the plugin those writes land in the GC heap -> heap
#                  corruption, which is what STATUS_HEAP_CORRUPTION (0xC0000374) at
#                  2026-11-23 19:25:52 and four access violations inside VCRUNTIME140
#                  look like. Tested with a 64 KB 0xCC guard region after each buffer.
#                  Needs no concurrency and cannot crash anything.
#
#   H2 (race)      Two threads in the SDK at once on one context make its state machine
#                  return "failed" with retError=0 and the output buffer untouched -
#                  exactly the symptom logged at 16:15:33, 18:13:22, 02:05:51 and
#                  09:53:43. Tested serially, then concurrently unlocked, then
#                  concurrently locked.
#
# Experiment hygiene: each thread gets its OWN output buffer, so the context is the only
# thing the two threads share. Otherwise a garbled buffer could be explained by two
# threads writing one scratch area, which is a different bug.
#
# Safety
# ------
# Every SDK call used here is a READ: GetBatteryInfo, GetFreeSpaceInImages,
# GetStatusOfCards, SS_GetCapability, ISO_GetCapability. No exposure is taken, no shutter
# is actuated, no camera setting is written. The only state changed is an open PTP
# session, closed on exit.
#
# Usage
# -----
#   powershell -ExecutionPolicy Bypass -File .\lumix-race-probe.ps1 -Phase 0,1
#   powershell -ExecutionPolicy Bypass -File .\lumix-race-probe.ps1          (all phases)
#   powershell -ExecutionPolicy Bypass -File .\lumix-race-probe.ps1 -Calls 20000
#   powershell -ExecutionPolicy Bypass -File .\lumix-race-probe.ps1 -Phase 4
#
# -Phase is taken as TEXT and split here, because powershell.exe -File mangles a comma
# list bound to [int[]]: "-Phase 0,1" arrives as just 1, which silently skips phase 0.
# Measured on 2026-11-24: -File gives 1, -Command gives 0,1. Parsing it ourselves makes
# both invocations behave the same.
#
# If the probe dies during phase 3, that is itself the positive result for H2. Every line
# is flushed to the log as it is written, so a crash loses nothing. Re-run with -Phase 4
# afterwards to get the control.
# =========================================================================================
[CmdletBinding()]
param(
    [string]$Phase = '0,1,2,3,4',
    [int]$Calls   = 2000,
    # Watchdog for the concurrent phases. The unlocked phase deadlocks rather than failing,
    # so without this the probe sits silent forever instead of reporting the deadlock.
    [int]$TimeoutSec = 30,
    [string]$TetherDir = 'C:\Program Files\Panasonic\LUMIX Tether',
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

# See the -Phase note in the header: parse it here so -File and -Command agree.
$PhaseList = @($Phase -split '[,;\s]+' | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ })
if ($PhaseList.Count -eq 0) {
    Write-Host ('ABORT: -Phase "' + $Phase + '" parsed to nothing. Use e.g. -Phase 0,1') -ForegroundColor Red
    exit 1
}

if (-not $LogPath) {
    $LogPath = Join-Path ([Environment]::GetFolderPath('Desktop')) ('lumix-race-probe-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
}

function Write-Log {
    param([string]$Text = '', [string]$Colour = 'Gray')
    Write-Host $Text -ForegroundColor $Colour
    Add-Content -LiteralPath $LogPath -Value $Text -Encoding UTF8
}

# --- refuse to run against a live NINA: the PTP session is exclusive -----------------------
$nina = Get-Process NINA -ErrorAction SilentlyContinue
if ($nina) {
    Write-Host ('ABORT: NINA is running (pid ' + ($nina.Id -join ', ') + '). Close it first.') -ForegroundColor Red
    exit 1
}
$dllPath = Join-Path $TetherDir 'Lmxptpif.dll'
if (-not (Test-Path $dllPath)) {
    Write-Host ('ABORT: no Lmxptpif.dll under ' + $TetherDir) -ForegroundColor Red
    exit 1
}

# =========================================================================================
# C# 5 only - Windows PowerShell 5.1 compiles Add-Type with CodeDom, so no string
# interpolation, no out-var, no expression-bodied members.
# =========================================================================================
$cs = @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

public class LumixProbe {

    const string DLL = "Lmxptpif.dll";
    const CallingConvention CC = CallingConvention.Winapi;
    const byte TRUE_ = 1;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetDllDirectory(string path);

    // Module init. The plugin calls this once from LumixcameraProvider's constructor, before
    // any enumeration, and its own comment records what happens if it is skipped or fails:
    // "showed up only as Found 0 Lumix Cameras". The first draft of this probe omitted it and
    // got exactly that - GetPnPDeviceInfo ret=0 err=0 count=0 - on a camera NINA connects to
    // fine. void, no arguments, entry point is the name itself.
    [DllImport(DLL, ExactSpelling = true, CallingConvention = CC)]
    static extern void LMX_func_api_Init();

    [DllImport(DLL, EntryPoint = "LMX_func_api_GetPnPDeviceInfo", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_GetPnPDeviceInfo(IntPtr devBuf, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_SelectPnPDevice", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_SelectPnPDevice(uint index, IntPtr devBuf, IntPtr ctxOut, uint z4, IntPtr z5, uint z6, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_OpenSession", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_OpenSession(uint connectVer, out uint devConnectVer, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_CloseSession", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_CloseSession(IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_CloseDevice", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_CloseDevice(IntPtr ctx, out uint retError);

    [DllImport(DLL, EntryPoint = "LMX_func_api_GetBatteryInfo", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_GetBatteryInfo(IntPtr buf, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_GetFreeSpaceInImages", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_GetFreeSpaceInImages(IntPtr buf, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_GetStatusOfCards", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_GetStatusOfCards(IntPtr buf, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_SS_GetCapability", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_SS_GetCapability(IntPtr buf, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_ISO_GetCapability", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_ISO_GetCapability(IntPtr buf, IntPtr ctx, out uint retError);

    // Body sizes exactly as the plugin allocates them; each gets its own guard region.
    public const int CTX_BODY = 8192;     // Tether.cs: _ctxBacking = new byte[8192]
    public const int CAP_BODY = 16384;    // Tether.cs: _capBacking = new byte[16384]
    public const int DEV_BODY = 262144;   // Tether.cs: _devBacking = new byte[262144]
    public const int GUARD    = 65536;
    const byte GUARD_BYTE = 0xCC;
    const int  WORD = 32;                 // TETHER_WORD_SCRATCH_BYTES in the plugin

    static IntPtr _ctx, _dev, _capA, _capB;
    static readonly object _gate = new object();

    public static long Calls, FailNoError, ZeroBuffer, OtherFail;

    public static string Init(string tetherDir) {
        if (!SetDllDirectory(tetherDir)) { return "SetDllDirectory failed"; }
        _ctx  = Marshal.AllocHGlobal(CTX_BODY + GUARD);
        _dev  = Marshal.AllocHGlobal(DEV_BODY + GUARD);
        _capA = Marshal.AllocHGlobal(CAP_BODY + GUARD);
        _capB = Marshal.AllocHGlobal(CAP_BODY + GUARD);
        // First P/Invoke, so this is also where a DLL-load or bitness problem surfaces.
        try {
            LMX_func_api_Init();
        } catch (Exception ex) {
            return "LMX_func_api_Init threw: " + ex.GetType().Name + ": " + ex.Message;
        }
        return null;
    }

    static void Fill(IntPtr p, int offset, int count, byte v) {
        for (int i = 0; i < count; i++) { Marshal.WriteByte(p, offset + i, v); }
    }

    /// Pre-connect only: zero every body AND arm every guard.
    public static void ArmAll() {
        Fill(_ctx,  0, CTX_BODY, 0); Fill(_ctx,  CTX_BODY, GUARD, GUARD_BYTE);
        Fill(_dev,  0, DEV_BODY, 0); Fill(_dev,  DEV_BODY, GUARD, GUARD_BYTE);
        Fill(_capA, 0, CAP_BODY, 0); Fill(_capA, CAP_BODY, GUARD, GUARD_BYTE);
        Fill(_capB, 0, CAP_BODY, 0); Fill(_capB, CAP_BODY, GUARD, GUARD_BYTE);
    }

    /// Post-connect: re-arm the guards ONLY. The context body is live SDK state and
    /// zeroing it would break the session - the original draft of this script did exactly
    /// that, which would have invalidated every phase after the first.
    public static void ReArmGuards() {
        Fill(_ctx,  CTX_BODY, GUARD, GUARD_BYTE);
        Fill(_dev,  DEV_BODY, GUARD, GUARD_BYTE);
        Fill(_capA, CAP_BODY, GUARD, GUARD_BYTE);
        Fill(_capB, CAP_BODY, GUARD, GUARD_BYTE);
    }

    static int GuardFirst(IntPtr p, int body) {
        for (int i = 0; i < GUARD; i++) { if (Marshal.ReadByte(p, body + i) != GUARD_BYTE) { return i; } }
        return -1;
    }
    static int GuardLast(IntPtr p, int body) {
        int last = -1;
        for (int i = 0; i < GUARD; i++) { if (Marshal.ReadByte(p, body + i) != GUARD_BYTE) { last = i; } }
        return last;
    }

    public static int CtxGuardFirst()  { return GuardFirst(_ctx, CTX_BODY); }
    public static int CtxGuardLast()   { return GuardLast(_ctx, CTX_BODY); }
    public static int DevGuardFirst()  { return GuardFirst(_dev, DEV_BODY); }
    public static int CapAGuardFirst() { return GuardFirst(_capA, CAP_BODY); }
    public static int CapBGuardFirst() { return GuardFirst(_capB, CAP_BODY); }

    // ---- connect -------------------------------------------------------------------------
    public static string Connect(out string model) {
        model = "";
        uint err;
        ArmAll();

        byte r = Ext_GetPnPDeviceInfo(_dev, out err);
        int count = Marshal.ReadInt32(_dev, 0);
        if (r != TRUE_ || count <= 0) {
            return "GetPnPDeviceInfo ret=" + r + " err=0x" + err.ToString("X8") + " count=" + count;
        }
        // Device-info layout (Tether, array-max 64): count@0, IDs@8,
        // LMX_DEVINFO[64]@520 stride 1036, dev_Index@entry+0, model name (Unicode)@entry+520.
        int devIndex = Marshal.ReadInt32(_dev, 520);
        model = Marshal.PtrToStringUni(new IntPtr(_dev.ToInt64() + 520 + 520));

        // THE measurement for H1: SelectPnPDevice is the call that FILLS the context.
        r = Ext_SelectPnPDevice((uint)devIndex, _dev, _ctx, 0, IntPtr.Zero, 0, out err);
        if (r != TRUE_) { return "SelectPnPDevice ret=" + r + " err=0x" + err.ToString("X8"); }

        uint devVer;
        Ext_OpenSession(0x00010001, out devVer, _ctx, out err);
        return null;
    }

    public static void Disconnect() {
        uint err;
        try { Ext_CloseSession(_ctx, out err); } catch (Exception) { }
        try { Ext_CloseDevice(_ctx, out err); } catch (Exception) { }
    }

    // ---- a single read call, with the plugin's own success test ---------------------------
    // TryReadTetherWord clears the first 32 bytes, calls, and treats ret != 1 as "the
    // camera did not report". Replicated exactly, so a hit here means the same thing as
    // the warning line in NINA's log.
    static byte Dispatch(int which, IntPtr buf, out uint err) {
        switch (which) {
            case 0:  return Ext_GetBatteryInfo(buf, _ctx, out err);
            case 1:  return Ext_GetFreeSpaceInImages(buf, _ctx, out err);
            case 2:  return Ext_GetStatusOfCards(buf, _ctx, out err);
            case 3:  return Ext_SS_GetCapability(buf, _ctx, out err);
            default: return Ext_ISO_GetCapability(buf, _ctx, out err);
        }
    }

    static void OneCall(int which, IntPtr buf, bool useLock) {
        if (useLock) { lock (_gate) { OneCallInner(which, buf); } }
        else { OneCallInner(which, buf); }
    }

    static void OneCallInner(int which, IntPtr buf) {
        uint err = 0;
        for (int i = 0; i < WORD; i++) { Marshal.WriteByte(buf, i, 0); }
        byte r = Dispatch(which, buf, out err);

        bool allZero = true;
        for (int i = 0; i < WORD; i++) { if (Marshal.ReadByte(buf, i) != 0) { allZero = false; break; } }

        Interlocked.Increment(ref Calls);
        if (r != TRUE_) {
            if (err == 0) { Interlocked.Increment(ref FailNoError); }
            else { Interlocked.Increment(ref OtherFail); }
        }
        if (allZero) { Interlocked.Increment(ref ZeroBuffer); }
    }

    public static void ResetCounters() { Calls = 0; FailNoError = 0; ZeroBuffer = 0; OtherFail = 0; }

    /// Phase 1: one call to each export, single-threaded, reporting what it actually does.
    /// Without this, a 100% failure rate in phase 3 could just mean "this export is broken".
    public static string ProbeOne(int which) {
        uint err = 0;
        for (int i = 0; i < WORD; i++) { Marshal.WriteByte(_capA, i, 0); }
        byte r = Dispatch(which, _capA, out err);
        byte[] head = new byte[WORD];
        Marshal.Copy(_capA, head, 0, WORD);
        return "ret=" + r + " err=0x" + err.ToString("X8") + " head=" + BitConverter.ToString(head, 0, 16);
    }

    public static void RunSerial(int n) {
        for (int i = 0; i < n; i++) { OneCall(i % 5, _capA, false); }
    }

    // Measured on 2026-11-24 10:32: with no lock, two threads wedge inside the SDK and never
    // return - the first run of this probe hung for over 77 s with no crash and no WER report,
    // against a 6.2 s serial baseline for the same 2000 calls. A thread stuck in a native call
    // cannot be aborted, so the wedged threads stay wedged and the SDK session stays unusable
    // for the rest of the process: phase 4 must run in a FRESH process after a body power-cycle.
    // The watchdog exists so the probe REPORTS the deadlock, with the call count reached, instead
    // of sitting silent.
    public static bool RunConcurrent(int n, bool useLock, int timeoutMs) {
        int half = n / 2;
        ThreadStart a = delegate {
            // thread A: capability reads, as the sequencer drives them during capture
            for (int i = 0; i < half; i++) { OneCall(3 + (i % 2), _capA, useLock); }
        };
        ThreadStart b = delegate {
            // thread B: battery / card words, as NINA's 2-second device poll drove them
            for (int i = 0; i < half; i++) { OneCall(i % 3, _capB, useLock); }
        };
        Thread ta = new Thread(a); ta.IsBackground = true;
        Thread tb = new Thread(b); tb.IsBackground = true;
        ta.Start(); tb.Start();
        bool okA = ta.Join(timeoutMs);
        bool okB = tb.Join(timeoutMs);
        return okA && okB;
    }
}
'@

Add-Type -TypeDefinition $cs -Language CSharp

Write-Log '=== Lumix SDK race / overflow probe ===' 'Cyan'
Write-Log ('host       : ' + $env:COMPUTERNAME)
Write-Log ('when       : ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Write-Log ('tether dll : ' + $dllPath)
Write-Log ('dll stamp  : ' + (Get-Item $dllPath).LastWriteTime)
Write-Log ('calls/phase: ' + $Calls)
Write-Log ('phases     : ' + ($PhaseList -join ','))
Write-Log ('log        : ' + $LogPath)
Write-Log

$initErr = [LumixProbe]::Init($TetherDir)
if ($initErr) { Write-Log ('ABORT: ' + $initErr) 'Red'; exit 1 }
Write-Log 'sdk init   : LMX_func_api_Init returned' 'Green'

$model = ''
$connErr = [LumixProbe]::Connect([ref]$model)
if ($connErr) {
    Write-Log ('ABORT: connect failed - ' + $connErr) 'Red'
    Write-Log ''
    Write-Log 'count=0 after a successful Init means the DLL ran but found no camera. Check, in order:' 'Yellow'
    Write-Log '  1. is LUMIX Tether (the Panasonic app) open? it claims the device exclusively' 'Yellow'
    Write-Log '  2. is the body in PC(Tether) mode rather than PC(Storage)?' 'Yellow'
    Write-Log '  3. did a crashed NINA leave a half-open PTP session? power-cycle the body' 'Yellow'
    Write-Log ('  4. is this shell 64-bit? ' + [Environment]::Is64BitProcess) 'Yellow'
    exit 1
}
Write-Log ('connected  : ' + $model) 'Green'
Write-Log

# ---------- PHASE 0: H1, the context overflow ---------------------------------------------
if ($PhaseList -contains 0) {
    Write-Log '--- PHASE 0   H1: did SelectPnPDevice write past the 8192-byte context? ---' 'Cyan'
    $cf = [LumixProbe]::CtxGuardFirst()
    $cl = [LumixProbe]::CtxGuardLast()
    $df = [LumixProbe]::DevGuardFirst()
    if ($cf -lt 0) {
        Write-Log '  ctx guard INTACT - the context fits in 8192 bytes.' 'Green'
        Write-Log '  H1 is NOT supported by this call. The overflow, if any, is elsewhere.' 'Green'
    } else {
        $need = [LumixProbe]::CTX_BODY + $cl + 1
        Write-Log ('  ctx guard BREACHED - first changed byte at body+' + $cf + ', last at body+' + $cl) 'Red'
        Write-Log ('  => this context needs at least ' + $need + ' bytes. The plugin gives it 8192.') 'Red'
        Write-Log '  => in the plugin that buffer is a PINNED MANAGED ARRAY, so the overflow' 'Red'
        Write-Log '     lands in the GC heap. H1 DEMONSTRATED.' 'Red'
    }
    if ($df -ge 0) { Write-Log ('  dev guard breached at body+' + $df + ' (262144-byte buffer)') 'Red' }
    Write-Log
}

# ---------- PHASE 1: do the exports work at all, single-threaded? --------------------------
if ($PhaseList -contains 1) {
    Write-Log '--- PHASE 1   sanity: one single-threaded call to each export ---' 'Cyan'
    Write-Log '  ret=1 means the export works, so a failure later is about concurrency.'
    $names = @('GetBatteryInfo','GetFreeSpaceInImages','GetStatusOfCards','SS_GetCapability','ISO_GetCapability')
    for ($i = 0; $i -lt 5; $i++) {
        Write-Log ('  ' + $names[$i].PadRight(22) + ' ' + [LumixProbe]::ProbeOne($i))
    }
    Write-Log
}

# ---------- PHASES 2-4: serial, concurrent-unlocked, concurrent-locked ---------------------
function Invoke-ProbePhase {
    param([string]$Name, [string]$Expect, [scriptblock]$Body)
    Write-Log ('--- ' + $Name + ' ---') 'Cyan'
    Write-Log ('  expectation: ' + $Expect)
    [LumixProbe]::ResetCounters()
    [LumixProbe]::ReArmGuards()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $completed = [bool](& $Body | Select-Object -Last 1)
    $sw.Stop()
    if (-not $completed) {
        Write-Log ('  DEADLOCKED - threads did not return within ' + $TimeoutSec + 's.') 'Red'
        Write-Log ('  calls completed before wedging: ' + [LumixProbe]::Calls + ' of ' + $Calls) 'Red'
        Write-Log '  A thread stuck in a native call cannot be aborted, so the SDK session is now' 'Red'
        Write-Log '  unusable in this process. Any later phase here would be meaningless - run it' 'Red'
        Write-Log '  in a fresh process after power-cycling the body.' 'Red'
        Write-Log
        return -1
    }
    $c  = [LumixProbe]::Calls
    $fn = [LumixProbe]::FailNoError
    $zb = [LumixProbe]::ZeroBuffer
    $of = [LumixProbe]::OtherFail
    $rate = 0.0
    if ($c -gt 0) { $rate = [math]::Round(100.0 * $fn / $c, 3) }
    Write-Log ('  calls                       : ' + $c + '  in ' + [math]::Round($sw.Elapsed.TotalSeconds,1) + 's')
    Write-Log ('  ret!=1 with retError=0      : ' + $fn + '   (' + $rate + '%)   <-- the symptom in NINA''s log')
    Write-Log ('  ret!=1 with a real error    : ' + $of)
    Write-Log ('  output buffer left all-zero : ' + $zb)
    $g1 = [LumixProbe]::CtxGuardFirst(); if ($g1 -ge 0) { Write-Log ('  ctx guard breached at body+' + $g1) 'Red' }
    $g2 = [LumixProbe]::CapAGuardFirst(); if ($g2 -ge 0) { Write-Log ('  capA guard breached at body+' + $g2) 'Red' }
    $g3 = [LumixProbe]::CapBGuardFirst(); if ($g3 -ge 0) { Write-Log ('  capB guard breached at body+' + $g3) 'Red' }
    Write-Log
    return [int]$fn
}

$serial = $null; $unlocked = $null; $locked = $null

$timeoutMs = $TimeoutSec * 1000

if ($PhaseList -contains 2) {
    $serial = Invoke-ProbePhase 'PHASE 2   serial baseline, one thread' `
        'near zero - one thread cannot race itself.' `
        { [LumixProbe]::RunSerial($Calls); $true }
}

if ($PhaseList -contains 3) {
    Write-Log 'PHASE 3 drives two threads into the SDK on one shared context, with no lock.' 'Yellow'
    Write-Log 'This is the plugin exactly as deployed. On 2026-11-24 it DEADLOCKED here.' 'Yellow'
    $unlocked = Invoke-ProbePhase 'PHASE 3   concurrent, NO lock (the plugin as deployed)' `
        'H2 predicts the symptom or a deadlock here, and in no other phase.' `
        { [LumixProbe]::RunConcurrent($Calls, $false, $timeoutMs) }
}

if ($PhaseList -contains 4) {
    if ($unlocked -eq -1) {
        Write-Log 'SKIPPING PHASE 4: phase 3 wedged the SDK in this process, so a locked run here' 'Yellow'
        Write-Log 'would measure nothing. Power-cycle the body and run: -Phase 4' 'Yellow'
        Write-Log
    } else {
        $locked = Invoke-ProbePhase 'PHASE 4   concurrent, WITH one lock (the proposed fix)' `
            'back to the serial baseline, if a lock is the right fix.' `
            { [LumixProbe]::RunConcurrent($Calls, $true, $timeoutMs) }
    }
}

[LumixProbe]::Disconnect()

# ---------- verdict ------------------------------------------------------------------------
Write-Log '=== VERDICT ===' 'Cyan'
if ($null -ne $serial -and $unlocked -eq -1) {
    Write-Log 'H2 DEMONSTRATED, in its strongest form: two threads on one shared context' 'Green'
    Write-Log ('deadlocked inside the SDK, while the same ' + $Calls + ' calls on one thread completed') 'Green'
    Write-Log 'cleanly. This is the same shape as NINA stalling 148s at 02:05:51 and 82s at' 'Green'
    Write-Log '09:53:43 - NINA only escaped because its exposure timeout fired.' 'Green'
    Write-Log ''
    Write-Log 'Still NOT established: whether this race also causes the heap corruption' 'Yellow'
    Write-Log '(0xC0000374 at 19:25:52 and the VCRUNTIME140 access violations). A deadlock' 'Yellow'
    Write-Log 'and a corrupted heap are different failures.' 'Yellow'
    Write-Log ''
    Write-Log 'Next: power-cycle the body, then run -Phase 4 for the locked control.' 'Cyan'
} elseif ($null -ne $serial -and $null -ne $unlocked) {
    if ($unlocked -gt 0 -and $serial -eq 0) {
        Write-Log 'H2 DEMONSTRATED: the symptom appears only when two threads share the context.' 'Green'
        if ($null -ne $locked) {
            if ($locked -eq 0) {
                Write-Log 'And one lock removes it. That is the fix, measured rather than argued.' 'Green'
            } else {
                Write-Log ('Lock did NOT remove it (' + $locked + ' failures). A lock is not sufficient -') 'Red'
                Write-Log 'do not ship it as the fix on this evidence.' 'Red'
            }
        }
    } elseif ($unlocked -eq 0) {
        Write-Log 'H2 NOT reproduced at this call rate.' 'Yellow'
        Write-Log 'Before concluding anything: these are five 32-byte word reads, while the real' 'Yellow'
        Write-Log 'collision involved an 18 MB Get_Object held open for hundreds of milliseconds.' 'Yellow'
        Write-Log 'Re-run with a much larger -Calls before treating this as a negative.' 'Yellow'
    } else {
        Write-Log ('Symptom present in BOTH serial (' + $serial + ') and concurrent (' + $unlocked + ') runs -') 'Yellow'
        Write-Log 'so concurrency alone is not the cause. Check phase 1: an export may simply fail.' 'Yellow'
    }
} else {
    Write-Log 'Phases 2 and 3 are both needed for a verdict on H2.' 'Yellow'
}
Write-Log
Write-Log ('full log: ' + $LogPath) 'Cyan'
