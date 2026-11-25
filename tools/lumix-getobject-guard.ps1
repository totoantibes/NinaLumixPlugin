# Does the Tether SDK write outside the buffers the plugin gives it?
# ==================================================================================
# Two measurements, both needing one real frame, both on guarded native allocations so an
# overflow is observable instead of destructive. Nothing here touches the GC heap, so a
# breach is recorded rather than crashing the probe.
#
#  M1  Get_Object_DataSize writes 8 bytes, not 4.
#      Panasonic's header declares UINT64* pDataSize and the DLL does "mov qword ptr [r14],0"
#      at 0x180024DA0. The plugin declared "out uint" - a 4-byte destination - so the SDK wrote
#      4 bytes past it on every exposure, from inside the capture callback. This points the
#      pointer at a 4-byte body with a guard right after it and reports what actually lands
#      there. Expect: guard bytes 0..3 overwritten with the size's high half (zeros).
#      This is the DEMONSTRATION of a bug already fixed in the plugin, so a breach here is
#      the expected result and confirms the fix was needed.
#
#  M2  Does Get_Object honour storeBufSize?   <- THE OPEN QUESTION
#      The plugin hands it a managed byte[dataSize] via "ref buffer[0]" and tells it dataSize.
#      If the SDK overshoots by even one byte, that is a direct write past the end of an 18 MB
#      managed array - GC-heap corruption of exactly the shape seen in the crashes (four
#      VCRUNTIME140 access violations at four offsets, plus one STATUS_HEAP_CORRUPTION).
#      Expect if healthy: guard fully intact.
#
#  M0  Is the 262144-byte device buffer big enough?   <- already answered, reported positively
#      lumix-race-probe.ps1 armed a guard after this buffer and only printed a line when it was
#      BREACHED, so its clean run was a silent pass. Stated explicitly here.
#
# NINA must be closed: the SDK session is exclusive and NINA holds it.
#
# This fires the physical shutter once, using whatever shutter speed and save target the camera
# is already set to. It changes no camera setting. A long exposure just means a longer wait, so
# -TimeoutSec is adjustable.
[CmdletBinding()]
param(
    [string]$TetherDir = 'C:\Program Files\Panasonic\LUMIX Tether',
    [int]$TimeoutSec = 90,
    [int]$GuardBytes = 1048576
)

$ErrorActionPreference = 'Stop'

function Write-Log([string]$m, [string]$c = 'Gray') { Write-Host $m -ForegroundColor $c }

Write-Log '=== preflight ===' 'Cyan'
$nina = Get-Process NINA -ErrorAction SilentlyContinue
if ($nina) {
    Write-Log ('ABORT: NINA is running (pid ' + ($nina.Id -join ', ') + '). Close it - the SDK session is exclusive.') 'Red'
    exit 1
}
'NINA running  : False'
$dll = Join-Path $TetherDir 'Lmxptpif.dll'
if (-not (Test-Path $dll)) { Write-Log "ABORT: no Tether DLL at $dll" 'Red'; exit 1 }
('tether DLL    : ' + (Get-Item $dll).Length + ' bytes')
('guard size    : ' + $GuardBytes + ' bytes')
''

# =========================================================================================
# C# 5 only - Windows PowerShell 5.1 compiles Add-Type with CodeDom, so no string
# interpolation, no out-var, no expression-bodied members.
# =========================================================================================
$cs = @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

public class GetObjectGuard {

    const string DLL = "Lmxptpif.dll";
    const CallingConvention CC = CallingConvention.Winapi;
    const byte TRUE_ = 1;
    const byte GUARD_BYTE = 0xCC;
    const byte BODY_FILL  = 0xAA;

    // Exactly as the plugin allocates them.
    const int CTX_BODY = 8192;      // Tether.cs: _ctxBacking = new byte[8192]
    const int DEV_BODY = 262144;    // Tether.cs: _devBacking = new byte[262144]
    const int DEV_GUARD = 65536;
    const int EXT_DEVINFO_BASE = 520, EXT_DEVINFO_STRIDE = 1036;

    const uint EVENT_OBJCT_ADD          = 0x10000040;
    const uint EVENT_OBJCT_REQ_TRNSFER  = 0x10000043;
    const uint REC_CTRL_RELEASE_ONESHOT = 0x03000011;  // EVENT_ID_REC_CTRL_RELEASE (0x03000010) + 1

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetDllDirectory(string path);

    // Module init. Skipping it yields "GetPnPDeviceInfo ret=0 count=0" on a camera that works
    // fine in NINA - the first draft of the race probe omitted it and lost a session to that.
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

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int LMX_CALLBACK_FUNC(uint p1, uint p2);

    // No context argument on these two, in either mode - matches the plugin.
    [DllImport(DLL, EntryPoint = "LMX_func_api_Reg_NotifyCallback", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern uint Reg_NotifyCallback(uint callBackType, LMX_CALLBACK_FUNC appfunc);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Delete_CallBackInfo", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    static extern uint Delete_CallBackInfo(uint callBackType);

    [StructLayout(LayoutKind.Sequential)]
    public struct FORM_ENUM_UINT32 {
        public ushort NumOfVal;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public uint[] SupportVal;
        public byte Available;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct REC_CTRL {
        public uint CtrlID;
        public FORM_ENUM_UINT32 ParamData;
    }

    [DllImport(DLL, EntryPoint = "LMX_func_api_Rec_Ctrl_Release", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_Rec_Ctrl_Release(ref REC_CTRL lpRecCtrl, IntPtr ctx, out uint retError);

    // pDataSize as a RAW POINTER, so the probe decides exactly how many bytes the destination
    // has. That is the whole point of M1: a 4-byte body with a guard behind it.
    [DllImport(DLL, EntryPoint = "LMX_func_api_Get_Object_DataSize", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_GetObjectDataSize_Raw(uint objHandle, IntPtr pDataSize, IntPtr ctx, out uint retError);
    // And the correct declaration, as a cross-check that 64 bits is right.
    [DllImport(DLL, EntryPoint = "LMX_func_api_Get_Object_DataSize", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_GetObjectDataSize_U64(uint objHandle, out ulong pDataSize, IntPtr ctx, out uint retError);

    [DllImport(DLL, EntryPoint = "LMX_func_api_Get_Object_FormatType", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_GetObjectFormatType(uint objHandle, out uint pFormatType, IntPtr ctx, out uint retError);
    [DllImport(DLL, EntryPoint = "LMX_func_api_Get_Object", ExactSpelling = true, CallingConvention = CC)]
    static extern byte Ext_GetObject_Raw(uint objHandle, IntPtr storeBuf, uint storeBufSize, IntPtr ctx, out uint retError);

    static IntPtr _ctx, _dev;
    static int _guardBytes;
    static byte[] _guardPattern;

    // Kept in a static field for the lifetime of the process: the SDK holds a thunk to it, and a
    // collected delegate means the camera calls into freed memory.
    static LMX_CALLBACK_FUNC _cb;
    static ManualResetEvent _done = new ManualResetEvent(false);

    public static string Log = "";
    public static int DevGuardFirst = -1;

    // M1 results
    public static byte  M1_Ret;
    public static uint  M1_Err;
    public static string M1_Body = "";
    public static string M1_GuardHead = "";
    public static int   M1_GuardFirst = -1;
    public static ulong M1_Size64;
    public static byte  M1_Ret64;

    // M2 results
    public static uint  M2_ObjHandle;
    public static uint  M2_Format;
    public static ulong M2_DataSize;
    public static byte  M2_Ret;
    public static uint  M2_Err;
    public static int   M2_GuardFirst = -1;
    public static int   M2_GuardLast  = -1;
    public static string M2_Magic = "";
    public static bool  M2_Ran;

    static void Say(string s) { Log = Log + s + Environment.NewLine; }

    static void Fill(IntPtr p, int offset, int count, byte v) {
        byte[] b = new byte[count];
        for (int i = 0; i < count; i++) { b[i] = v; }
        Marshal.Copy(b, 0, new IntPtr(p.ToInt64() + offset), count);
    }

    static string Hex(IntPtr p, int offset, int count) {
        byte[] b = new byte[count];
        Marshal.Copy(new IntPtr(p.ToInt64() + offset), b, 0, count);
        return BitConverter.ToString(b);
    }

    /// First byte of the guard region that is no longer GUARD_BYTE, or -1 if untouched.
    static int GuardFirst(IntPtr p, int body, int guard) {
        byte[] b = new byte[guard];
        Marshal.Copy(new IntPtr(p.ToInt64() + body), b, 0, guard);
        for (int i = 0; i < guard; i++) { if (b[i] != GUARD_BYTE) { return i; } }
        return -1;
    }
    static int GuardLast(IntPtr p, int body, int guard) {
        byte[] b = new byte[guard];
        Marshal.Copy(new IntPtr(p.ToInt64() + body), b, 0, guard);
        int last = -1;
        for (int i = 0; i < guard; i++) { if (b[i] != GUARD_BYTE) { last = i; } }
        return last;
    }

    public static string Connect(string tetherDir, int guardBytes) {
        _guardBytes = guardBytes;
        _guardPattern = new byte[guardBytes];
        for (int i = 0; i < guardBytes; i++) { _guardPattern[i] = GUARD_BYTE; }

        if (!SetDllDirectory(tetherDir)) { return "SetDllDirectory failed"; }

        _ctx = Marshal.AllocHGlobal(CTX_BODY);
        _dev = Marshal.AllocHGlobal(DEV_BODY + DEV_GUARD);
        Fill(_ctx, 0, CTX_BODY, 0);
        Fill(_dev, 0, DEV_BODY, 0);
        Fill(_dev, DEV_BODY, DEV_GUARD, GUARD_BYTE);

        try { LMX_func_api_Init(); }
        catch (Exception ex) { return "LMX_func_api_Init threw: " + ex.GetType().Name + ": " + ex.Message; }

        uint err;
        byte r = Ext_GetPnPDeviceInfo(_dev, out err);
        uint count = (uint)Marshal.ReadInt32(_dev, 0);
        Say("GetPnPDeviceInfo ret=" + r + " err=" + err + " count=" + count);
        // M0: the plugin's 262144-byte device buffer, checked positively rather than silently.
        DevGuardFirst = GuardFirst(_dev, DEV_BODY, DEV_GUARD);
        if (r != TRUE_ || count == 0) { return "no camera found (ret=" + r + " count=" + count + ")"; }

        int devIndex = Marshal.ReadInt32(_dev, EXT_DEVINFO_BASE);
        r = Ext_SelectPnPDevice((uint)devIndex, _dev, _ctx, 0, IntPtr.Zero, 0, out err);
        Say("SelectPnPDevice idx=" + devIndex + " ret=" + r + " err=" + err);
        if (r != TRUE_) { return "SelectPnPDevice failed (ret=" + r + " err=" + err + ")"; }

        uint devVer;
        r = Ext_OpenSession(0x00010001, out devVer, _ctx, out err);
        Say("OpenSession ret=" + r + " err=" + err + " devVer=0x" + devVer.ToString("X"));
        if (r != TRUE_) { return "OpenSession failed (ret=" + r + " err=" + err + ")"; }

        _cb = new LMX_CALLBACK_FUNC(OnNotify);
        uint c1 = Reg_NotifyCallback(EVENT_OBJCT_REQ_TRNSFER, _cb);
        uint c2 = Reg_NotifyCallback(EVENT_OBJCT_ADD, _cb);
        Say("Reg_NotifyCallback REQ_TRNSFER=" + c1 + " OBJCT_ADD=" + c2);
        return null;
    }

    /// Runs on the SDK's own thread. The object is only guaranteed valid for this call, so the
    /// measurements happen here, exactly where the plugin's ExposureFinished does its work.
    static int OnNotify(uint eventType, uint param) {
        try {
            if (M2_Ran) { return 0; }
            M2_Ran = true;
            M2_ObjHandle = param;

            uint err;
            uint fmt;
            Ext_GetObjectFormatType(param, out fmt, _ctx, out err);
            M2_Format = fmt;

            // ---- M1: point a 4-byte destination at the SDK and see how much it writes ----
            IntPtr m1 = Marshal.AllocHGlobal(4 + 64);
            try {
                Fill(m1, 0, 4, BODY_FILL);
                Fill(m1, 4, 64, GUARD_BYTE);
                M1_Ret = Ext_GetObjectDataSize_Raw(param, m1, _ctx, out err);
                M1_Err = err;
                M1_Body = Hex(m1, 0, 4);
                M1_GuardHead = Hex(m1, 4, 8);
                M1_GuardFirst = GuardFirst(m1, 4, 64);
            } finally { Marshal.FreeHGlobal(m1); }

            // ---- the correct 64-bit declaration, as a cross-check ----
            ulong size64;
            M1_Ret64 = Ext_GetObjectDataSize_U64(param, out size64, _ctx, out err);
            M1_Size64 = size64;
            M2_DataSize = size64;

            // ---- M2: does Get_Object stay inside storeBufSize? ----
            if (size64 > 0 && size64 <= (256UL * 1024UL * 1024UL)) {
                int body = (int)size64;
                IntPtr m2 = Marshal.AllocHGlobal(body + _guardBytes);
                try {
                    Marshal.Copy(_guardPattern, 0, new IntPtr(m2.ToInt64() + body), _guardBytes);
                    M2_Ret = Ext_GetObject_Raw(param, m2, (uint)body, _ctx, out err);
                    M2_Err = err;
                    M2_Magic = Hex(m2, 0, 4);
                    M2_GuardFirst = GuardFirst(m2, body, _guardBytes);
                    M2_GuardLast  = GuardLast(m2, body, _guardBytes);
                } finally { Marshal.FreeHGlobal(m2); }
            }
        } catch (Exception ex) {
            Say("callback threw: " + ex.GetType().Name + ": " + ex.Message);
        } finally {
            _done.Set();
        }
        return 0;
    }

    public static string Shoot(int timeoutSec) {
        REC_CTRL rc = new REC_CTRL();
        rc.CtrlID = REC_CTRL_RELEASE_ONESHOT;
        rc.ParamData = new FORM_ENUM_UINT32();
        rc.ParamData.SupportVal = new uint[512];

        uint err;
        byte r = Ext_Rec_Ctrl_Release(ref rc, _ctx, out err);
        Say("Rec_Ctrl_Release(ONESHOT) ret=" + r + " err=" + err);
        if (r != TRUE_) { return "shutter release failed (ret=" + r + " err=" + err + ")"; }

        if (!_done.WaitOne(timeoutSec * 1000)) {
            return "no object event within " + timeoutSec + "s - is the camera on a long exposure, " +
                   "or is the mode dial somewhere the SDK cannot release?";
        }
        return null;
    }

    public static void Cleanup() {
        try { Delete_CallBackInfo(EVENT_OBJCT_REQ_TRNSFER); } catch (Exception) { }
        try { Delete_CallBackInfo(EVENT_OBJCT_ADD); } catch (Exception) { }
        uint err;
        try { Ext_CloseSession(_ctx, out err); } catch (Exception) { }
        try { Ext_CloseDevice(_ctx, out err); } catch (Exception) { }
    }
}
'@
Add-Type -TypeDefinition $cs -Language CSharp

Write-Log '=== connecting ===' 'Cyan'
$fail = [GetObjectGuard]::Connect($TetherDir, $GuardBytes)
[GetObjectGuard]::Log.TrimEnd() -split "`r?`n" | ForEach-Object { '  ' + $_ }
if ($fail) {
    Write-Log ('ABORT: ' + $fail) 'Red'
    [GetObjectGuard]::Cleanup()
    exit 1
}
''

Write-Log '--- M0: the 262144-byte device buffer ---' 'Cyan'
$dg = [GetObjectGuard]::DevGuardFirst
if ($dg -lt 0) {
    Write-Log '  INTACT - enumeration wrote nothing past 262144 bytes.' 'Green'
    Write-Log '  (lumix-race-probe.ps1 already showed this, but only by printing nothing.)'
} else {
    Write-Log ('  BREACHED at body+' + $dg + ' - the device buffer is UNDERSIZED.') 'Red'
}
''

Write-Log '=== firing one frame ===' 'Cyan'
Write-Log 'Uses the camera''s current shutter speed and save target; changes no setting.'
$before = [GetObjectGuard]::Log.Length
$fail = [GetObjectGuard]::Shoot($TimeoutSec)
[GetObjectGuard]::Log.Substring($before).TrimEnd() -split "`r?`n" | Where-Object { $_ } | ForEach-Object { '  ' + $_ }
if ($fail) {
    Write-Log ('ABORT: ' + $fail) 'Red'
    [GetObjectGuard]::Cleanup()
    exit 1
}
''

$h  = [GetObjectGuard]::M2_ObjHandle
$sz = [GetObjectGuard]::M2_DataSize
('object handle : 0x' + $h.ToString('X'))
('format        : 0x' + [GetObjectGuard]::M2_Format.ToString('X'))
('data size     : ' + $sz + ' bytes   (64-bit read, ret=' + [GetObjectGuard]::M1_Ret64 + ')')
''

Write-Log '--- M1: how many bytes does Get_Object_DataSize write? ---' 'Cyan'
('  ret=' + [GetObjectGuard]::M1_Ret + ' err=' + [GetObjectGuard]::M1_Err)
('  4-byte body  : ' + [GetObjectGuard]::M1_Body + '   (was AA-AA-AA-AA)')
('  next 8 bytes : ' + [GetObjectGuard]::M1_GuardHead + '   (guard, was CC-CC-...)')
$g1 = [GetObjectGuard]::M1_GuardFirst
if ($g1 -ge 0) {
    Write-Log ('  WROTE PAST THE 4-BYTE DESTINATION, first at body+' + $g1 + '.') 'Yellow'
    Write-Log '  Expected: this is the bug the plugin just fixed. pDataSize is a UINT64*, so an' 'Yellow'
    Write-Log '  "out uint" destination was overrun by 4 bytes on every single exposure.' 'Yellow'
} else {
    Write-Log '  Guard intact - only 4 bytes written.' 'Green'
    Write-Log '  That CONTRADICTS the header and the disassembly. Re-check before trusting it.' 'Yellow'
}
''

Write-Log '--- M2: does Get_Object honour storeBufSize? ---' 'Cyan'
('  ret=' + [GetObjectGuard]::M2_Ret + ' err=' + [GetObjectGuard]::M2_Err + '  magic=' + [GetObjectGuard]::M2_Magic)
$f = [GetObjectGuard]::M2_GuardFirst
$l = [GetObjectGuard]::M2_GuardLast
if (-not [GetObjectGuard]::M2_Ran -or $sz -eq 0) {
    Write-Log '  NOT MEASURED - no usable object arrived.' 'Yellow'
} elseif ($f -lt 0) {
    Write-Log ('  GUARD INTACT across all ' + $GuardBytes + ' bytes.') 'Green'
    Write-Log ('  Get_Object wrote nothing past the ' + $sz + ' bytes it was given. This is the') 'Green'
    Write-Log '  answer the survey was missing: the capture buffer is not the heap corruption.' 'Green'
} else {
    Write-Log ('  GUARD BREACHED: first changed byte at body+' + $f + ', last at body+' + $l) 'Red'
    Write-Log ('  Get_Object wrote ' + ($l + 1) + ' bytes past a buffer it was told was ' + $sz + ' bytes.') 'Red'
    Write-Log '  In the plugin that destination is a managed byte[], so this IS the heap' 'Red'
    Write-Log '  corruption behind the VCRUNTIME140 access violations. Do not image until fixed.' 'Red'
}
''

Write-Log '=== cleanup ===' 'Cyan'
[GetObjectGuard]::Cleanup()
'session closed, callbacks removed'
''
Write-Log 'Reminder: this probe held the SDK session. Restart NINA before imaging.' 'Cyan'
