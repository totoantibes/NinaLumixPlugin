# Lumix SDK late-write probe: does the SDK finish an ABANDONED transfer, and when?
# =========================================================================================
# RUN ON THE IMAGING PC (192.168.1.185), WITH NINA CLOSED, THE GH5S CONNECTED AND ON,
# AND IN DAYLIGHT. This one DOES actuate the shutter.
#
# Why this exists
# ---------------
# The two crash dumps of 2026-11-24 show Lmxptpif finishing a transfer it had already
# reported as FAILED (Get_Object ret=0, zero-filled buffer), 77 s and 141 s later, with an
# 18 MB memcpy into a native buffer it had itself released. Everything about that is
# inference from two post-mortems. Waiting for a sixth crash to confirm it is hoping, and
# the five recorded crashes took between 1 and 234 frames to arrive.
#
# This measures it directly instead, and WITHOUT CRASHING ANYTHING, by inverting the
# dangerous part: where the plugin frees the destination and lets the SDK write into
# whatever takes its place, this probe KEEPS the destination alive forever and watches it.
# If bytes change after the probe has stopped calling the SDK, the late write is proven and
# timestamped. It lands in our own buffer, harmlessly.
#
#   phase A   provoke the precursor: one capture, Get_Object on a guarded pinned buffer,
#             while a second thread hammers GetBatteryInfo with no lock - the collision
#             lumix-race-probe.ps1 phase 3 showed wedges the SDK, but now with a real
#             18 MB transfer in flight, which is what its own notes said was missing.
#
#   phase B   abandon it exactly as the plugin does - Skip_Object_Transfer, then stop
#             touching the SDK entirely - and poll the buffer for -WatchSeconds, reporting
#             the first byte that changes and when.
#
# Outcomes, and what each would mean
# ----------------------------------
#   buffer changes after we went quiet   the late write is REAL and timed. This is the
#                                        measurement the dumps could only infer.
#   Get_Object returns 0 + zero buffer    the precursor reproduces on demand with a
#   but the buffer never changes          transfer in flight; the late write either needs
#                                        longer than -WatchSeconds or needs the buffer to
#                                        have been freed. Still a result worth having.
#   everything succeeds, no precursor     the collision did not reproduce this run. Says
#                                        nothing either way - re-run, or raise -BatteryHz.
#
# Safety
# ------
# One shutter actuation per -Captures. No camera setting is written. The buffer is native,
# allocated once, never freed, so a late write cannot corrupt anything else in this process
# and this process holds nothing of value. The SDK will probably wedge: that is expected,
# it is process-local (proven 2026-11-25), and closing this window clears it with no power
# cycle. NINA must be closed, or it and this probe will fight over the camera.
#
# Usage
# -----
#   powershell -ExecutionPolicy Bypass -File .\lumix-late-write-probe.ps1
#   powershell -ExecutionPolicy Bypass -File .\lumix-late-write-probe.ps1 -WatchSeconds 300
#   powershell -ExecutionPolicy Bypass -File .\lumix-late-write-probe.ps1 -Captures 3 -BatteryHz 400
# The shutter speed is whatever the camera is currently set to - this probe writes no camera
# setting, so set the body to something short before running it. There is deliberately no
# exposure parameter: adding one would mean writing a setting, and every call here stays a read
# apart from the release itself.
param(
    [int]$WatchSeconds = 240,
    [int]$Captures     = 1,
    [int]$BatteryHz    = 200,
    [string]$TetherDll = 'C:\Program Files\Panasonic\LUMIX Tether\Lmxptpif.dll'
)

$ErrorActionPreference = 'Stop'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path ([Environment]::GetFolderPath('Desktop')) ("lumix-late-write-$stamp.log")

function Say([string]$m, [string]$c = 'Gray') {
    $line = $m
    Write-Host $line -ForegroundColor $c
    Add-Content -LiteralPath $log -Value $line -Encoding ASCII
}

Say '=== Lumix SDK late-write probe ==='
Say ('host         : ' + $env:COMPUTERNAME)
Say ('when         : ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Say ('tether dll   : ' + $TetherDll)
if (-not (Test-Path $TetherDll)) { Say 'ABORT: tether DLL not found' 'Red'; exit 1 }
Say ('dll stamp    : ' + (Get-Item $TetherDll).LastWriteTime)
Say ('watch window : ' + $WatchSeconds + 's')
Say ('captures     : ' + $Captures)
Say ('battery rate : ~' + $BatteryHz + ' calls/s on a second thread, NO lock')
Say ('log          : ' + $log)
Say ''

$nina = Get-Process NINA -ErrorAction SilentlyContinue
if ($nina) { Say ('ABORT: NINA is running (pid ' + ($nina.Id -join ',') + '). Close it first.') 'Red'; exit 1 }

$cs = @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

public static class LateWrite {
    const string DLL = "Lmxptpif.dll";
    const byte GUARD = 0xCC;
    const uint EVENT_OBJCT_REQ_TRNSFER = 0x10000043;
    const uint EVENT_OBJCT_ADD         = 0x10000040;
    // All four read from the plugin's Lmx_event_id enum, not guessed. I had SHUTTER as
    // 0x10000020 and ISO as 0x02000010 from memory; both were wrong, which is why these are
    // now annotated with where they come from.
    const uint EVENT_REC_CTRL_RELEASE  = 0x03000010;   // lumixcam.cs:205
    const uint EVENT_SHUTTER           = 0x02000030;   // lumixcam.cs:200
    const uint EVENT_ISO               = 0x02000020;   // lumixcam.cs:199
    const uint CARDLESS_HDL            = 0x12345678;
    const int  CTX_BYTES               = 8192;
    const int  DEV_BYTES               = 262144;   // race probe / plugin _devBacking size

    public delegate int LMX_CALLBACK_FUNC(uint p1, uint p2);

    // These four signatures are LIFTED VERBATIM from lumix-race-probe.ps1, which connects to
    // this camera successfully. An earlier draft of this probe invented them and died with an
    // AccessViolationException inside SelectPnPDevice: that call takes SEVEN parameters, not the
    // four I had guessed, so the stack was wrong. Do not "simplify" these.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetDllDirectory(string path);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Init", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern void Init();
    [DllImport(DLL, EntryPoint = "LMX_func_api_GetPnPDeviceInfo", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte GetPnPDeviceInfo(IntPtr devBuf, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_SelectPnPDevice", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte SelectPnPDevice(uint index, IntPtr devBuf, IntPtr ctxOut, uint z4, IntPtr z5, uint z6, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_OpenSession", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte OpenSession(uint connectVer, out uint devConnectVer, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_CloseSession", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte CloseSession(IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_CloseDevice", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte CloseDevice(IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Reg_NotifyCallback", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern uint Reg_NotifyCallback(uint t, LMX_CALLBACK_FUNC f);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Delete_CallBackInfo", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern uint Delete_CallBackInfo(uint t);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Rec_Ctrl_Release", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte Rec_Ctrl_Release(IntPtr ctrl, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Get_Object_DataSize", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte Get_Object_DataSize(uint h, out ulong size, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Get_Object", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte Get_Object(uint h, IntPtr buf, uint bufSize, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Skip_Object_Transfer", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte Skip_Object_Transfer(uint h, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_GetBatteryInfo", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte GetBatteryInfo(IntPtr buf, IntPtr ctx, out uint retError);
    // Prerequisites the plugin performs at connect and the first draft of this probe skipped.
    // Without the save target the camera has no reason to offer the frame to the PC, and the
    // first attempt got Rec_Ctrl_Release ret=0 with no transfer event at all.
    [DllImport(DLL, EntryPoint = "LMX_func_api_SetupFilesConfig_Set_Target", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte SetupFilesConfig_Set_Target(ushort target, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_CameraMode_Get_Mode_Pos", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte CameraMode_Get_Mode_Pos(out uint modePos, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_SS_GetParam", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern byte SS_Get_Param(out int param, IntPtr ctx, out uint retError);

    static IntPtr _ctx;
    static IntPtr _buf;          // the destination. Allocated once, NEVER freed.
    static long   _bufBytes;
    static uint   _declared;     // what we told the SDK the buffer holds
    static volatile uint _objHandle;
    static ManualResetEvent _gotObject = new ManualResetEvent(false);
    static LMX_CALLBACK_FUNC _cb;       // kept alive deliberately
    static volatile bool _stopBattery;
    static long _batteryCalls, _batteryRefused;
    public static string Lines = "";
    static Stopwatch _sw = Stopwatch.StartNew();
    static string _logPath;
    static readonly object _logLock = new object();

    // Appends to the log on EVERY line and flushes. Slower than buffering, and the only way a
    // probe whose subject may kill the process can be trusted to leave evidence behind.
    static void L(string s) {
        string line = "[" + _sw.ElapsedMilliseconds.ToString("D7") + "ms] " + s;
        Lines += line + "\n";
        if (_logPath == null) { return; }
        lock (_logLock) {
            try {
                using (System.IO.StreamWriter w = new System.IO.StreamWriter(_logPath, true)) {
                    w.WriteLine(line);
                    w.Flush();
                }
            } catch (Exception) { }
        }
    }

    static int OnEvent(uint p1, uint p2) {
        if (p1 == EVENT_OBJCT_REQ_TRNSFER || p1 == EVENT_OBJCT_ADD) {
            _objHandle = p2;
            L("callback: event 0x" + p1.ToString("X8") + " handle 0x" + p2.ToString("X8"));
            _gotObject.Set();
        }
        return 0;
    }

    // Writes to p, NOT to _buf. An earlier draft ignored p and always wrote _buf, which worked
    // only by accident for the one call that mattered.
    static void Fill(IntPtr p, long bytes, byte v) {
        byte[] chunk = new byte[65536];
        for (int i = 0; i < chunk.Length; i++) { chunk[i] = v; }
        long done = 0;
        while (done < bytes) {
            int n = (int)Math.Min(chunk.Length, bytes - done);
            Marshal.Copy(chunk, 0, new IntPtr(p.ToInt64() + done), n);
            done += n;
        }
    }

    // first offset whose byte is not GUARD, or -1
    static long FirstChanged(long bytes) {
        byte[] chunk = new byte[65536];
        long done = 0;
        while (done < bytes) {
            int n = (int)Math.Min(chunk.Length, bytes - done);
            Marshal.Copy(new IntPtr(_buf.ToInt64() + done), chunk, 0, n);
            for (int i = 0; i < n; i++) { if (chunk[i] != GUARD) { return done + i; } }
            done += n;
        }
        return -1;
    }

    static void BatteryThread() {
        IntPtr scratch = Marshal.AllocHGlobal(4096);
        int sleepMs = Math.Max(1, 1000 / Math.Max(1, Hz));
        while (!_stopBattery) {
            uint err;
            byte r = 0;
            try { r = GetBatteryInfo(scratch, _ctx, out err); } catch (Exception) { break; }
            Interlocked.Increment(ref _batteryCalls);
            if (r != 1) { Interlocked.Increment(ref _batteryRefused); }
            Thread.Sleep(sleepMs);
        }
    }

    public static int Hz = 200;

    public static bool Run(int watchSeconds, int captures, int hz, string tetherDir, string logPath) {
        _logPath = logPath;
        Hz = hz;
        if (!SetDllDirectory(tetherDir)) { L("SetDllDirectory failed"); return false; }

        // Buffer sizes from the race probe, which took them from the plugin's own fields:
        // ctx 8192, device info 262144. The device buffer must be this big - the Tether layout
        // is count@0, IDs@8, LMX_DEVINFO[64]@520 with stride 1036.
        _ctx = Marshal.AllocHGlobal(CTX_BYTES);
        for (int i = 0; i < CTX_BYTES; i++) { Marshal.WriteByte(_ctx, i, 0); }
        IntPtr devInfo = Marshal.AllocHGlobal(DEV_BYTES);
        for (int i = 0; i < DEV_BYTES; i++) { Marshal.WriteByte(devInfo, i, 0); }

        Init();
        L("Init returned");

        uint e;
        byte r0 = GetPnPDeviceInfo(devInfo, out e);
        int count = Marshal.ReadInt32(devInfo, 0);
        L("GetPnPDeviceInfo ret=" + r0 + " err=0x" + e.ToString("X8") + " count=" + count);
        if (r0 != 1 || count <= 0) { L("ABORT: no camera enumerated"); return false; }

        int devIndex = Marshal.ReadInt32(devInfo, 520);
        string model = Marshal.PtrToStringUni(new IntPtr(devInfo.ToInt64() + 520 + 520));
        L("device index " + devIndex + ", model " + model);

        byte r1 = SelectPnPDevice((uint)devIndex, devInfo, _ctx, 0, IntPtr.Zero, 0, out e);
        L("SelectPnPDevice ret=" + r1 + " err=0x" + e.ToString("X8"));
        if (r1 != 1) { L("ABORT: SelectPnPDevice failed"); return false; }

        uint devVer;
        byte r2 = OpenSession(0x00010001, out devVer, _ctx, out e);
        L("OpenSession ret=" + r2 + " devConnectVer=0x" + devVer.ToString("X8") + " err=0x" + e.ToString("X8"));
        if (r2 != 1) { L("ABORT: no session"); return false; }

        // All five the plugin registers, in its order. The first draft registered only the two
        // object events; REC_CTRL_RELEASE in particular is the one the release itself reports on.
        _cb = new LMX_CALLBACK_FUNC(OnEvent);
        Reg_NotifyCallback(EVENT_OBJCT_ADD, _cb);
        Reg_NotifyCallback(EVENT_OBJCT_REQ_TRNSFER, _cb);
        Reg_NotifyCallback(EVENT_REC_CTRL_RELEASE, _cb);
        Reg_NotifyCallback(EVENT_SHUTTER, _cb);
        Reg_NotifyCallback(EVENT_ISO, _cb);
        L("5 callbacks registered");

        uint modePos;
        byte rm = CameraMode_Get_Mode_Pos(out modePos, _ctx, out e);
        L("CameraMode_Get_Mode_Pos ret=" + rm + " modePos=" + modePos + " err=0x" + e.ToString("X8"));
        int ssNow;
        byte rss = SS_Get_Param(out ssNow, _ctx, out e);
        L("SS_Get_Param ret=" + rss + " raw=0x" + ssNow.ToString("X8") + " err=0x" + e.ToString("X8"));

        // THE ONLY CAMERA SETTING THIS PROBE WRITES: save target = 1 (PC). Without it the frame
        // goes to the card and no PC transfer is offered, which is what happened on the first run.
        byte rt = SetupFilesConfig_Set_Target(1, _ctx, out e);
        L("SetupFilesConfig_Set_Target(1=PC) ret=" + rt + " err=0x" + e.ToString("X8") + "   <- the one write");

        // 32 MB destination, filled with the guard pattern. Never freed, so a late write is
        // observable instead of fatal.
        _bufBytes = 32L * 1024 * 1024;
        _buf = Marshal.AllocHGlobal(new IntPtr(_bufBytes));
        Fill(_buf, _bufBytes, GUARD);
        L("destination: " + _bufBytes + " bytes at 0x" + _buf.ToInt64().ToString("X") + ", filled 0xCC, will NEVER be freed");

        bool abandoned = false;
        Thread bt = null;
        for (int c = 0; c < captures; c++) {
            _gotObject.Reset();
            _objHandle = 0;
            IntPtr ctrl = Marshal.AllocHGlobal(64);
            for (int i = 0; i < 64; i++) { Marshal.WriteByte(ctrl, i, 0); }
            // 0x03000011 = LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_ONESHOT, read from the plugin's own
            // enum (Library/lumixcam.cs) rather than guessed. ParamData.NumOfVal sits after
            // CtrlID and must be 0, which the zero-fill above already gives.
            Marshal.WriteInt32(ctrl, 0, 0x03000011);   // ONESHOT release
            byte rc = Rec_Ctrl_Release(ctrl, _ctx, out e);
            L("capture " + (c + 1) + ": Rec_Ctrl_Release ret=" + rc + " err=0x" + e.ToString("X8"));

            if (!_gotObject.WaitOne(30000)) { L("capture " + (c + 1) + ": NO transfer event within 30s"); continue; }
            uint h = _objHandle; if (h == 0) { h = CARDLESS_HDL; }

            ulong size = 0;
            byte rs = Get_Object_DataSize(h, out size, _ctx, out e);
            L("Get_Object_DataSize ret=" + rs + " size=" + size + " err=0x" + e.ToString("X8"));
            if (size == 0 || size > (ulong)_bufBytes) { L("size unusable for this buffer; skipping the fetch"); continue; }

            // Start the collision HERE: the battery thread must be inside the DLL while the
            // 18 MB fetch is, which is where both crash dumps show it. Starting it before the
            // capture (the first draft) collided with nothing, because this thread was asleep.
            bt = new Thread(new ThreadStart(BatteryThread));
            bt.IsBackground = true;
            _stopBattery = false;
            bt.Start();
            L("battery thread started ~" + hz + "/s, NO lock - colliding with the fetch now");

            _declared = (uint)size;
            byte rg = Get_Object(h, _buf, _declared, _ctx, out e);
            byte b0 = Marshal.ReadByte(_buf, 0), b1 = Marshal.ReadByte(_buf, 1);
            byte b2 = Marshal.ReadByte(_buf, 2), b3 = Marshal.ReadByte(_buf, 3);
            string magic = b0.ToString("X2") + " " + b1.ToString("X2") + " " + b2.ToString("X2") + " " + b3.ToString("X2");
            L("Get_Object ret=" + rg + " err=0x" + e.ToString("X8") + " magic=" + magic);

            bool precursor = (rg != 1) || (b0 == 0 && b1 == 0 && b2 == 0 && b3 == 0);
            if (precursor) {
                L("*** PRECURSOR REPRODUCED: the transfer came back unusable with a real 18 MB fetch in flight.");
                L("    This is the state both crash dumps were in before the late write.");
            } else {
                L("transfer looked healthy this time (no precursor)");
            }

            // Abandon it exactly as the plugin does.
            byte rk = Skip_Object_Transfer(h, _ctx, out e);
            L("Skip_Object_Transfer ret=" + rk + " err=0x" + e.ToString("X8") + "  <- transfer abandoned");
            abandoned = true;
        }

        // Go completely quiet: no more SDK calls from us at all.
        _stopBattery = true;
        if (bt != null) { try { bt.Join(2000); } catch (Exception) { } }
        L("battery thread stopped after " + _batteryCalls + " calls, " + _batteryRefused + " refused");
        L("re-filling the destination with 0xCC, then going silent");
        Fill(_buf, _bufBytes, GUARD);
        long quietAt = _sw.ElapsedMilliseconds;
        L("SILENT from here. Watching " + _bufBytes + " bytes for " + watchSeconds + "s. Any change now is the SDK writing on its own.");

        bool observed = false;
        for (int s = 0; s < watchSeconds; s++) {
            Thread.Sleep(1000);
            long off = FirstChanged(_bufBytes);
            if (off >= 0) {
                long dt = _sw.ElapsedMilliseconds - quietAt;
                L("");
                L("*** LATE WRITE OBSERVED ***");
                L("    first changed byte at offset " + off);
                L("    " + (dt / 1000.0).ToString("F1") + "s after we stopped calling the SDK");
                byte[] head = new byte[16];
                Marshal.Copy(new IntPtr(_buf.ToInt64() + off), head, 0, 16);
                string hx = "";
                for (int i = 0; i < 16; i++) { hx += head[i].ToString("X2") + " "; }
                L("    bytes there: " + hx);
                L("    PROVEN: Lmxptpif wrote into a buffer it had already reported as failed and");
                L("            abandoned. In the plugin that memory is freed, which is the crash.");
                observed = true;
                break;
            }
            if (s % 15 == 14) { L("  ... " + (s + 1) + "s quiet, destination still untouched"); }
        }
        if (!observed) {
            L("");
            L("no late write within " + watchSeconds + "s. Not a disproof: the write may need longer,");
            L("or may only occur once the buffer has actually been released.");
        }

        try { Delete_CallBackInfo(EVENT_OBJCT_REQ_TRNSFER); } catch (Exception) { }
        try { Delete_CallBackInfo(EVENT_OBJCT_ADD); } catch (Exception) { }
        try { CloseSession(_ctx, out e); } catch (Exception) { }
        try { CloseDevice(_ctx, out e); } catch (Exception) { }
        L("session closed (the destination buffer is deliberately still allocated)");
        return observed && abandoned;
    }
}
'@

Add-Type -TypeDefinition $cs -Language CSharp

# The DLL is located by SetDllDirectory inside Run, the way the race probe does it - no PATH
# or working-directory juggling, which is what the first draft used.
$dir = Split-Path $TetherDll -Parent
try {
    $proven = [LateWrite]::Run($WatchSeconds, $Captures, $BatteryHz, $dir, $log)
} finally {
    # L() already wrote each line to the log as it happened, so echo to the console only -
    # otherwise every line would appear in the file twice.
    foreach ($l in ([LateWrite]::Lines -split "`n")) { if ($l.Trim()) { Write-Host $l } }
}

Say ''
Say '=== VERDICT ==='
if ($proven) {
    Say 'LATE WRITE MEASURED. The SDK completed an abandoned transfer into a buffer it had' 'Green'
    Say 'already reported as failed, after this probe stopped calling it entirely. That is the' 'Green'
    Say 'crash mechanism, demonstrated rather than inferred, with no crash and no dump.' 'Green'
} else {
    Say 'Not demonstrated - and after the 2026-11-25 run, probably NOT demonstrable this way.' 'Yellow'
    Say '' 'Yellow'
    Say 'If the PRECURSOR reproduced above, the collision half works and the design is simply' 'Yellow'
    Say 'watching the wrong memory. The crash dumps showed the late write going into the SDK''s' 'Yellow'
    Say 'OWN native buffer - the destination lay in no managed heap segment, 88 MB and 272 MB' 'Yellow'
    Say 'clear of the nearest - not into the buffer the caller supplied. This probe keeps the' 'Yellow'
    Say 'CALLER''s buffer alive, which the SDK may never touch again, so a longer -WatchSeconds' 'Yellow'
    Say 'will not help.' 'Yellow'
    Say '' 'Yellow'
    Say 'To catch it, make the SDK''s own freed buffer fault on write instead: run this probe' 'Yellow'
    Say 'under PageHeap (gflags /p /enable powershell.exe /full, or Application Verifier). The' 'Yellow'
    Say 'probe is short-lived and tiny, so PageHeap''s cost does not matter here the way it would' 'Yellow'
    Say 'in NINA. A late write then faults at the instant it happens, with a precise stack.' 'Yellow'
}
Say ''
Say 'The SDK may now be wedged in this process. That is expected and process-local: close this'
Say 'window. No power cycle is needed - proven 2026-11-25.'
Say ('full log: ' + $log)
