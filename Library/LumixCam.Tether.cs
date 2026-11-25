using System;
using System.Runtime.InteropServices;
using Roberthasson.NINA.Lumixcamera;   // NativeBinding

namespace LumixWrapper {

    /// <summary>
    /// Extended (LUMIX Tether DLL) call path. Each public LMX_func_api_* method here dispatches to either
    /// the bundled public SDK entry point (Pub_*) or the Tether DLL entry point (Ext_*), based on
    /// <see cref="NativeBinding.ExtendedMode"/>.
    ///
    /// The Tether ABI differs from the public headers in two systematic ways:
    ///   * 28 core functions are CamelCase (OpenSession vs Open_Session) — handled by EntryPoint=.
    ///   * post-connection ops take an EXTRA device-context pointer inserted just before retError.
    /// The context is produced by the 7-arg SelectPnPDevice at connect and held in <see cref="_tetherCtx"/>.
    /// </summary>
    public unsafe partial class LumixCam {

        // ---- device context (filled by the extended connect, threaded into every extended op) ----
        // One gate for every native call into Lmxptpif.dll.
        //
        // The Tether DLL keeps its session state in the single context buffer below, and that
        // context is threaded into 30 entry points. Driving two of them at once wedges the DLL:
        // measured on 2026-11-24 against a DC-GH5S, 2000 mixed read calls on one thread finished
        // in 6.3s with zero failures, the same 2000 split across two threads deadlocked inside the
        // DLL and never returned, and the same two threads with this lock finished in 6.8s with
        // zero failures. That deadlock is the shape of NINA stalling 148s at 2026-11-24 02:05:51
        // and 82s at 09:53:43, each time with a battery read and an image fetch logged in the same
        // millisecond.
        //
        // Held for one native call only. The bulb hold is an await between two separate
        // Rec_Ctrl_Release calls, so it never sits inside the gate; the longest hold is a
        // Get_Object transfer, about 130ms, against a 2s device-poll interval.
        private static readonly object _sdkGate = new object();
        
        private static byte[] _ctxBacking;
        private static GCHandle _ctxPin;
        private static IntPtr _tetherCtx = IntPtr.Zero;

        private static byte[] _capBacking;
        private static GCHandle _capPin;
        private static IntPtr _capBuf = IntPtr.Zero;   // large scratch for *_GetCapability (avoids OOB)

        private static byte[] _devBacking;
        private static GCHandle _devPin;
        private static IntPtr _devBuf = IntPtr.Zero;    // extended device-info buffer (array-max 64 layout)

        // ---- live-view buffers ----
        // Same rule as the object buffer below: this SDK writes into buffers after the call that supplied
        // them has returned, and on 2026-11-23 at 16:15:34 it faulted doing exactly that with a live-view
        // frame. All four destinations in Get_LiveView_data - the JPEG and the three info structs - were
        // locals passed by `ref`, pinned only for the duration of the call. They are now one permanently
        // pinned block.
        //
        // The info structs get a fixed 4 KB slot each rather than Marshal.SizeOf: the managed declarations in
        // this binding have already proved narrower than what the Tether DLL actually writes (see the
        // capability getters, which copy out of a large scratch for that reason), and over-allocating a few KB
        // costs nothing next to another out-of-bounds write. The three are write-only - nothing reads them
        // back - so their layout does not matter, only that the SDK has room.
        private const int LV_INFO_SLOT_BYTES = 4096;
        private static byte[] _lvBacking;
        private static GCHandle _lvPin;
        private static IntPtr _lvHist, _lvPosture, _lvLevel, _lvJpeg;

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_LiveView_data", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Get_LiveView_data_Ptr(
            IntPtr pHistgram, out uint pHistgramSize,
            IntPtr pPosture, out uint pPostureSize,
            IntPtr pLevel, out uint pLevelSize,
            IntPtr pJpeg, out uint pJpegSize, IntPtr ctx, out uint retError);

        private static bool EnsureLiveViewBuffers() {
            if (_lvBacking != null) { return true; }
            try {
                _lvBacking = new byte[3 * LV_INFO_SLOT_BYTES + (int)LMX_DEF_LIVEVIEW_STREAMDATA_SIZE_MAX];
                _lvPin = GCHandle.Alloc(_lvBacking, GCHandleType.Pinned);
                IntPtr b = _lvPin.AddrOfPinnedObject();
                _lvHist = b;
                _lvPosture = b + LV_INFO_SLOT_BYTES;
                _lvLevel = b + 2 * LV_INFO_SLOT_BYTES;
                _lvJpeg = b + 3 * LV_INFO_SLOT_BYTES;
                return true;
            } catch (Exception) {
                _lvBacking = null;
                return false;
            }
        }

        /// <summary>Offset of the JPEG frame within the buffer <see cref="Ext_TryGetLiveViewData"/> returns.</summary>
        public static int LiveViewJpegOffset => 3 * LV_INFO_SLOT_BYTES;

        /// <summary>Extended-mode live view, reading into the permanently pinned block. The array handed back
        /// is that block: the frame starts at <see cref="LiveViewJpegOffset"/> and stays valid only until the
        /// next call, so copy out anything that must outlive it. false in standard mode, where the caller must
        /// use the ref-based overload against the public SDK instead.</summary>
        public static bool Ext_TryGetLiveViewData(out byte[] block, out uint jpegSize, out uint retError) {
            block = null;
            jpegSize = 0;
            retError = 0;
            if (!NativeBinding.ExtendedMode) { return false; }
            // Gated like every other native call in this file. This wrapper was written before the gate
            // existed, and verify-no-ungated-path.ps1 caught it as the one remaining ungated path into
            // Lmxptpif. It matters more than most: live view polls this every 20 ms, so an ungated read here
            // is precisely the concurrent request the 2026-11-24 crash dumps implicate when one lands inside
            // an image transfer.
            lock (_sdkGate) {
                EnsureTetherBuffers();
                if (!EnsureLiveViewBuffers()) { return false; }
                byte r = Ext_Get_LiveView_data_Ptr(_lvHist, out _, _lvPosture, out _, _lvLevel, out _,
                                                   _lvJpeg, out jpegSize, _tetherCtx, out retError);
                block = _lvBacking;
                return r == LMX_BOOL_TRUE;
            }
        }

        // ---- object-transfer buffer ----
        // The SDK writes into buffers AFTER the call that supplied them has returned: on 2026-11-23 at
        // 16:15:34 Lmxptpif's own thread faulted in memcpy writing a 23,881-byte frame into a 1.4 MB region
        // that was already MEM_FREE / PAGE_NOACCESS. So an object transfer must never be given a fresh managed
        // array by `ref`: that array is pinned only for the duration of the call and is collectible the moment
        // the download completes. It gets the same treatment as the context and capability buffers above -
        // allocated once, pinned, and alive for the life of the process.
        //
        // Growing it does NOT free the old one. A buffer the SDK may still hold a pointer to must stay valid
        // forever, so retired handles are parked in _retiredObjPins and deliberately leaked.
        //
        // How much that leaks: one buffer per NEW MAXIMUM frame size, not one per body. RW2 size varies with
        // scene content on every frame - a 131-frame session on 2026-11-25 ranged over 18,681,856 to
        // 19,097,600 bytes - so the buffer is replaced each time a frame beats the largest seen so far. That
        // is the number of record values in the sequence, which grows like log(frames): expect a handful a
        // session, so of the order of 100 MB held for the life of the process. Bounded and acceptable, but
        // not the single buffer an earlier version of this comment claimed.
        private static byte[] _objBacking;
        private static GCHandle _objPin;
        private static readonly System.Collections.Generic.List<GCHandle> _retiredObjPins = new System.Collections.Generic.List<GCHandle>();

        /// <summary>The pinned object-transfer buffer, at least <paramref name="bytes"/> long. false if it
        /// could not be sized. The array is handed back so a caller can inspect the header without copying,
        /// but it MUST NOT be passed to anything that outlives the next transfer - the next frame overwrites
        /// it. Copy out what needs to live longer.</summary>
        public static bool TryGetObjectBuffer(uint bytes, out byte[] buf) {
            buf = null;
            if (bytes == 0) { return false; }
            try {
                if (_objBacking == null || _objBacking.Length < bytes) {
                    if (_objPin.IsAllocated) { _retiredObjPins.Add(_objPin); }   // leaked on purpose; see above
                    _objBacking = new byte[bytes];
                    _objPin = GCHandle.Alloc(_objBacking, GCHandleType.Pinned);
                }
                buf = _objBacking;
                return true;
            } catch (Exception) {
                return false;   // an allocation failure must not throw out of the SDK's callback thread
            }
        }

        private static void EnsureTetherBuffers() {
            lock (_sdkGate) {
                if (_tetherCtx == IntPtr.Zero) {
                    _ctxBacking = new byte[8192]; _ctxPin = GCHandle.Alloc(_ctxBacking, GCHandleType.Pinned);
                    _tetherCtx = _ctxPin.AddrOfPinnedObject();
                }
                if (_capBuf == IntPtr.Zero) {
                    _capBacking = new byte[16384]; _capPin = GCHandle.Alloc(_capBacking, GCHandleType.Pinned);
                    _capBuf = _capPin.AddrOfPinnedObject();
                }
                if (_devBuf == IntPtr.Zero) {
                    _devBacking = new byte[262144]; _devPin = GCHandle.Alloc(_devBacking, GCHandleType.Pinned);
                    _devBuf = _devPin.AddrOfPinnedObject();
                }
            }
        }

        // Device-info (Tether layout, array-max 64): count@0, IDs=IntPtr[64]@8, LMX_DEVINFO[64]@520 (stride 1036),
        // dev_Index@entry+0, dev_ModelName (Unicode, 256) @entry+4.
        private const int EXT_DEVINFO_BASE = 520, EXT_DEVINFO_STRIDE = 1036, EXT_MODELNAME_OFF = 520;

        // ===================== Extended externs (Tether DLL) =====================
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_GetPnPDeviceInfo", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_GetPnPDeviceInfo(IntPtr devBuf, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SelectPnPDevice", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_SelectPnPDevice(uint index, IntPtr devBuf, IntPtr ctxOut, uint z4, IntPtr z5, uint z6, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_OpenSession", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_OpenSession(uint connectVer, out uint devConnectVer, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_CloseSession", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_CloseSession(IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_CloseDevice", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_CloseDevice(IntPtr ctx, out uint retError);

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_GetCapability", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_SS_GetCapability(IntPtr capaBuf, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_GetParam", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_SS_GetParam(out int pulParam, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SS_SetParam", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_SS_SetParam(long ulParam, IntPtr ctx, out uint retError);

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ISO_GetCapability", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ISO_GetCapability(IntPtr capaBuf, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ISO_GetParam", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ISO_GetParam(out uint pulParam, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ISO_SetParam", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ISO_SetParam(uint ulParam, IntPtr ctx, out uint retError);

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_CameraMode_GetCapability", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_CameraMode_GetCapability(IntPtr capaBuf, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_CameraMode_Get_Mode_Pos", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_CameraMode_GetModePos(out uint pulParam, IntPtr ctx, out uint retError);

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Rec_Ctrl_Release", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Rec_Ctrl_Release(ref LMX_STRUCT_REC_CTRL lpRecCtrl, IntPtr ctx, out uint retError);

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Get_Object(uint objHandle, ref byte lpStoreBuf, uint storeBufSize, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object_FormatType", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Get_Object_FormatType(uint objHandle, out uint pFormatType, IntPtr ctx, out uint retError);
        // pDataSize is a UINT64*, not a UINT32*. Panasonic's header says so:
        //   UINT8 LMX_func_api_Get_Object_DataSize(UINT32 ObjHandle, UINT64* pDataSize, UINT32 *retError)
        // and the Tether DLL's own code confirms it - at 0x180024DA0 it does "mov qword ptr [r14],0"
        // through this pointer, an 8-byte write. Declared "out uint", the SDK wrote 4 bytes past a
        // 4-byte destination on every exposure, from inside the capture callback. It is the only
        // UINT64* in the whole 84-prototype API, which is how it survived this long.
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object_DataSize", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Get_Object_DataSize(uint objHandle, out ulong pDataSize, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object_FileName", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Get_Object_FileName(uint objHandle, ref LMX_STRUCT_PTP_ARRAY_STRING pFileName, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Skip_Object_Transfer", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Skip_Object_Transfer(uint objHandle, IntPtr ctx, out uint retError);

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Ctrl_LiveView_Start", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Ctrl_LiveView_Start(IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Ctrl_LiveView_Stop", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Ctrl_LiveView_Stop(IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_LiveView_data", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Get_LiveView_data(
            ref LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM pHistgram, out uint pHistgramSize,
            ref LMX_STRUCT_LIVEVIEW_INFO_POSTURE pPosture, out uint pPostureSize,
            ref LMX_STRUCT_LIVEVIEW_INFO_LEVEL pLevel, out uint pLevelSize,
            ref byte pJpeg, out uint pJpegSize, IntPtr ctx, out uint retError);

        // ===================== Public dispatchers (driver calls these unqualified names) =====================
        public static byte LMX_func_api_SS_Get_Param(out int pulParam, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_SS_GetParam(out pulParam, _tetherCtx, out retError) : Pub_SS_Get_Param(out pulParam, out retError);
            }
        }
        public static byte LMX_func_api_SS_Set_Param(long ulParam, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_SS_SetParam(ulParam, _tetherCtx, out retError) : Pub_SS_Set_Param(ulParam, out retError);
            }
        }
        public static byte LMX_func_api_ISO_Get_Param(out uint pulParam, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_ISO_GetParam(out pulParam, _tetherCtx, out retError) : Pub_ISO_Get_Param(out pulParam, out retError);
            }
        }
        public static byte LMX_func_api_ISO_Set_Param(uint ulParam, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_ISO_SetParam(ulParam, _tetherCtx, out retError) : Pub_ISO_Set_Param(ulParam, out retError);
            }
        }
        public static byte LMX_func_api_Rec_Ctrl_Release(ref LMX_STRUCT_REC_CTRL lpRecCtrl, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_Rec_Ctrl_Release(ref lpRecCtrl, _tetherCtx, out retError) : Pub_Rec_Ctrl_Release(ref lpRecCtrl, out retError);
            }
        }
        public static byte LMX_func_api_Get_Object(uint objHandle, ref byte lpStoreBuf, uint storeBufSize, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_Get_Object(objHandle, ref lpStoreBuf, storeBufSize, _tetherCtx, out retError) : Pub_Get_Object(objHandle, ref lpStoreBuf, storeBufSize, out retError);
            }
        }
        public static byte LMX_func_api_Get_Object_FormatType(uint objHandle, out uint pFormatType, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_Get_Object_FormatType(objHandle, out pFormatType, _tetherCtx, out retError) : Pub_Get_Object_FormatType(objHandle, out pFormatType, out retError);
            }
        }
        public static byte LMX_func_api_Get_Object_DataSize(uint objHandle, out ulong pDataSize, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_Get_Object_DataSize(objHandle, out pDataSize, _tetherCtx, out retError) : Pub_Get_Object_DataSize(objHandle, out pDataSize, out retError);
            }
        }
        public static byte LMX_func_api_Get_Object_FileName(uint objHandle, ref LMX_STRUCT_PTP_ARRAY_STRING pFileName, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_Get_Object_FileName(objHandle, ref pFileName, _tetherCtx, out retError) : Pub_Get_Object_FileName(objHandle, ref pFileName, out retError);
            }
        }
        public static byte LMX_func_api_Skip_Object_Transfer(uint objHandle, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_Skip_Object_Transfer(objHandle, _tetherCtx, out retError) : Pub_Skip_Object_Transfer(objHandle, out retError);
            }
        }
        public static byte LMX_func_api_Ctrl_LiveView_Start(out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_Ctrl_LiveView_Start(_tetherCtx, out retError) : Pub_Ctrl_LiveView_Start(out retError);
            }
        }
        public static byte LMX_func_api_Ctrl_LiveView_Stop(out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_Ctrl_LiveView_Stop(_tetherCtx, out retError) : Pub_Ctrl_LiveView_Stop(out retError);
            }
        }

        public static byte LMX_func_api_Get_LiveView_data(
            ref LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM pHistgram, out uint pHistgramSize,
            ref LMX_STRUCT_LIVEVIEW_INFO_POSTURE pPosture, out uint pPostureSize,
            ref LMX_STRUCT_LIVEVIEW_INFO_LEVEL pLevel, out uint pLevelSize,
            ref byte pJpeg, out uint pJpegSize, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode
                ? Ext_Get_LiveView_data(ref pHistgram, out pHistgramSize, ref pPosture, out pPostureSize, ref pLevel, out pLevelSize, ref pJpeg, out pJpegSize, _tetherCtx, out retError)
                : Pub_Get_LiveView_data(ref pHistgram, out pHistgramSize, ref pPosture, out pPostureSize, ref pLevel, out pLevelSize, ref pJpeg, out pJpegSize, out retError);
            }
        }

        // Capability getters: extended path writes into a large scratch buffer, then copies the leading
        // portion (matching layout: NumOfVal@0, SupportVal@4...) into the managed struct — avoids OOB.
        public static byte LMX_func_api_SS_Get_Capability(ref LMX_STRUCT_SS_CAPA_INFO pInfo, out uint retError) {
            lock (_sdkGate) {
                if (!NativeBinding.ExtendedMode) return Pub_SS_Get_Capability(ref pInfo, out retError);
                EnsureTetherBuffers();
                byte r = Ext_SS_GetCapability(_capBuf, _tetherCtx, out retError);
                pInfo = Marshal.PtrToStructure<LMX_STRUCT_SS_CAPA_INFO>(_capBuf);
                return r;
            }
        }
        public static byte LMX_func_api_ISO_Get_Capability(ref LMX_STRUCT_ISO_CAPA_INFO pInfo, out uint retError) {
            lock (_sdkGate) {
                if (!NativeBinding.ExtendedMode) return Pub_ISO_Get_Capability(ref pInfo, out retError);
                EnsureTetherBuffers();
                byte r = Ext_ISO_GetCapability(_capBuf, _tetherCtx, out retError);
                pInfo = Marshal.PtrToStructure<LMX_STRUCT_ISO_CAPA_INFO>(_capBuf);
                return r;
            }
        }
        // Dedicated mode-position getter — avoids the CameraMode capability struct whose Tether-buffer layout
        // differs from the managed struct (so reading CurVal_mode_pos from it gives the wrong value).
        public static byte LMX_func_api_CameraMode_Get_Mode_Pos(out uint pulParam, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_CameraMode_GetModePos(out pulParam, _tetherCtx, out retError) : Pub_CameraMode_Get_Mode_Pos(out pulParam, out retError);
            }
        }

        public static byte LMX_func_api_CameraMode_Get_Capability(ref LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO pInfo, out uint retError) {
            lock (_sdkGate) {
                if (!NativeBinding.ExtendedMode) return Pub_CameraMode_Get_Capability(ref pInfo, out retError);
                EnsureTetherBuffers();
                byte r = Ext_CameraMode_GetCapability(_capBuf, _tetherCtx, out retError);
                pInfo = Marshal.PtrToStructure<LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO>(_capBuf);
                return r;
            }
        }

        // ===================== Extended-only features (image quality, battery) =====================
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ImageInfo_Set_ImageQuality", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ImageInfo_SetImageQuality(uint quality, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ImageInfo_Get_ImageQuality", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ImageInfo_GetImageQuality(out uint quality, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ImageInfo_GetCapability", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ImageInfo_GetCapability(IntPtr buf, IntPtr ctx, out uint retError);
        // Declared with the buffer shape and read through TryReadTetherWord, like the card calls below: this
        // export is undocumented and had never actually been called, so there is no working behaviour here to
        // preserve — only a signature nobody had tested.
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_GetBatteryInfo", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_GetBatteryInfo(IntPtr buf, IntPtr ctx, out uint retError);

        // GH5S image-quality map (may vary per body): 0=Fine, 3=RAW, 4=RAW+Fine.
        public const uint IMGQ_JPEG_FINE = 0, IMGQ_RAW = 3, IMGQ_RAW_JPEG = 4;

        /// <summary>Set still image quality (extended only). Refreshes the range first (like SS/BULB).</summary>
        public static byte Ext_SetImageQuality(uint quality, out uint retError) {
            lock (_sdkGate) {
                EnsureTetherBuffers();
                Ext_ImageInfo_GetCapability(_capBuf, _tetherCtx, out _);
                return Ext_ImageInfo_SetImageQuality(quality, _tetherCtx, out retError);
            }
        }
        public static byte Ext_GetImageQuality(out uint quality, out uint retError) {
            lock (_sdkGate) {
                return Ext_ImageInfo_GetImageQuality(out quality, _tetherCtx, out retError);
            }
        }
        /// <summary>Battery reading. <paramref name="level"/> is the uint32 at offset 8 of the struct and the
        /// field to use; <paramref name="levelAlt"/> is the uint16 at offset 12, the other candidate, which only
        /// a reading at some level other than 100% can rule out; <paramref name="head"/> is the first 32 bits,
        /// logged so the struct stays visible. false when the call itself fails.</summary>
        public static bool Ext_TryGetBatteryInfo(out uint level, out uint levelAlt, out uint head, out uint retError, out string rawHead) {
            lock (_sdkGate) {
                level = 0;
                levelAlt = 0;
                bool ok = TryReadTetherWord(TetherWord.BatteryInfo, out head, out retError, out rawHead);
                if (!ok) { return false; }
                level = ScratchUInt32(8);
                levelAlt = ScratchUInt16(12);
                return true;
            }
        }

        // The optional variant, for the mid-session refresh: a battery reading is never worth waiting for.
        // If the gate is not free then the SDK is busy with something that matters more than a percentage -
        // an image transfer, most likely - so report gateBusy and let the caller skip this round rather than
        // queue a second request behind it. `lock` inside Ext_TryGetBatteryInfo re-enters on this thread, so
        // the gate is held exactly once for the duration of the native call either way.
        private const int BATTERY_GATE_WAIT_MS = 50;
        public static bool Ext_TryGetBatteryInfoIfFree(out uint level, out uint levelAlt, out uint head,
                                                       out uint retError, out string rawHead, out bool gateBusy) {
            level = 0;
            levelAlt = 0;
            head = 0;
            retError = 0;
            rawHead = "";
            gateBusy = false;
            bool held = false;
            try {
                System.Threading.Monitor.TryEnter(_sdkGate, BATTERY_GATE_WAIT_MS, ref held);
                if (!held) { gateBusy = true; return false; }
                return Ext_TryGetBatteryInfo(out level, out levelAlt, out head, out retError, out rawHead);
            } finally {
                if (held) { System.Threading.Monitor.Exit(_sdkGate); }
            }
        }

        // Read a field out of the scratch the last TryReadTetherWord filled. Bounds-checked: these offsets come
        // from an observed layout, not a documented one.
        private static uint ScratchUInt32(int offset) {
            if (_capBacking == null || offset + 4 > TETHER_WORD_SCRATCH_BYTES) { return 0; }
            return (uint)(_capBacking[offset] | (_capBacking[offset + 1] << 8) | (_capBacking[offset + 2] << 16) | (_capBacking[offset + 3] << 24));
        }

        private static uint ScratchUInt16(int offset) {
            if (_capBacking == null || offset + 2 > TETHER_WORD_SCRATCH_BYTES) { return 0; }
            return (uint)(_capBacking[offset] | (_capBacking[offset + 1] << 8));
        }

        // ===================== Save destination (SetupFilesConfig target) =====================
        // 0=SD only (default), 1=PC only (cardless), 2=PC+SD. On capture with a PC target the camera fires
        // OBJCT_REQ_TRNSFER with the cardless handle; Get_Object(CARDLESS_TRNSFER_HDL) pulls the frame to the PC.
        public const ushort SAVE_TARGET_SD = 0, SAVE_TARGET_PC = 1, SAVE_TARGET_PC_AND_SD = 2;
        public const uint CARDLESS_TRNSFER_HDL = 0x12345678;   // LMX_DEF_OBJ_CARDLESS_TRNSFER_HDL

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SetupFilesConfig_Get_Target", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_SetupFilesConfig_GetTarget(out ushort punParam, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SetupFilesConfig_Set_Target", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_SetupFilesConfig_SetTarget(ushort unParam, IntPtr ctx, out uint retError);

        public static byte LMX_func_api_SetupFilesConfig_Get_Target(out ushort punParam, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_SetupFilesConfig_GetTarget(out punParam, _tetherCtx, out retError) : Pub_SetupFilesConfig_Get_Target(out punParam, out retError);
            }
        }
        public static byte LMX_func_api_SetupFilesConfig_Set_Target(ushort unParam, out uint retError) {
            lock (_sdkGate) {
                return NativeBinding.ExtendedMode ? Ext_SetupFilesConfig_SetTarget(unParam, _tetherCtx, out retError) : Pub_SetupFilesConfig_Set_Target(unParam, out retError);
            }
        }

        // ===================== Card capacity and battery (extended only) =====================
        // The public SDK headers carry no storage or battery call at all; the Tether DLL exports several, all
        // undocumented. They are declared with the BUFFER shape (IntPtr, not `out uint`) and handed the 16 KB
        // pinned scratch, so that a call writing a struct rather than a single word cannot overrun anything,
        // and the first bytes come back as hex on every read.
        //
        // That hex is what settled the layouts, measured on a DC-BGH1 in extended mode on 2026-11-23:
        //
        //   GetBatteryInfo        11-00-00-16 08-00-00-00 64-00-00-00 64-00-FF-FF   then all zero
        //
        // The card calls were then read twice with the save target unchanged on PC only, once with the slot
        // empty and once with a card in it:
        //
        //   slot empty   free FF-FF-FF-FF (0xFFFFFFFF)   status 02-00-00-00 (0x00000002)
        //   card in      free 10-0A-00-00 (2576)         status 02-00-01-00 (0x00010002)
        //
        // So the body reports capacity whatever the save target is, 0xFFFFFFFF means no card rather than a
        // refusal, and in the status word the high 16 bits carry card presence while the low 16 were 2 in both.
        // So the battery call is NOT a single word: its first 32 bits are 0x16000011, which is not a level.
        // 0x64 = 100 - the level of the dummy battery in the body - appears twice, as a uint32 at offset 8 and
        // as a uint16 at offset 12, with 8 at offset 4 looking like a size or count. Offset 8 is read as the
        // level and offset 12 reported alongside it, because a reading at 100% cannot tell the two apart.
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_GetFreeSpaceInImages", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_GetFreeSpaceInImages(IntPtr buf, IntPtr ctx, out uint retError);

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_GetStatusOfCards", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_GetStatusOfCards(IntPtr buf, IntPtr ctx, out uint retError);

        /// <summary>Frames the camera says still fit on the card, as it reports them - including
        /// <see cref="CARD_FRAMES_NO_NUMBER"/> when it declines to give one. false when the call itself fails
        /// (standard mode, a body without the export, a refused call). Advisory: report it, never gate a
        /// capture on it.</summary>
        public static bool Ext_TryGetFreeSpaceInImages(out uint images, out uint retError, out string rawHead) {
            lock (_sdkGate) {
                return TryReadTetherWord(TetherWord.FreeSpaceInImages, out images, out retError, out rawHead);
            }
        }

        /// <summary>What the body returns from GetFreeSpaceInImages instead of a count. Measured as the
        /// card-absent reading; whether a card that is present but full reports it too is not yet known.</summary>
        public const uint CARD_FRAMES_NO_NUMBER = uint.MaxValue;

        /// <summary>Whether the status word says a card is in the slot: 0x00000002 with the slot empty and
        /// 0x00010002 with a card in it, so presence rides in the high 16 bits. Two samples only — a locked or
        /// unreadable card may well read differently, so callers must still handle "a card is there but the
        /// camera gives no usable capacity".</summary>
        public static bool CardPresent(uint statusWord) => (statusWord >> 16) != 0;

        /// <summary>Per-card status word as the Tether DLL reports it. Layout is undocumented; the raw bytes
        /// come back alongside so a log can pin it down.</summary>
        public static bool Ext_TryGetStatusOfCards(out uint status, out uint retError, out string rawHead) {
            lock (_sdkGate) {
                return TryReadTetherWord(TetherWord.StatusOfCards, out status, out retError, out rawHead);
            }
        }

        private enum TetherWord { FreeSpaceInImages, StatusOfCards, BatteryInfo }

        private static bool TryReadTetherWord(TetherWord which, out uint value, out uint retError, out string rawHead) {
            lock (_sdkGate) {
                value = 0;
                retError = 0;
                rawHead = "";
                if (!NativeBinding.ExtendedMode) { return false; }
                try {
                    EnsureTetherBuffers();
                    if (_capBuf == IntPtr.Zero || _capBacking == null) { return false; }
                    Array.Clear(_capBacking, 0, TETHER_WORD_SCRATCH_BYTES);
                    byte r;
                    switch (which) {
                        case TetherWord.FreeSpaceInImages: r = Ext_GetFreeSpaceInImages(_capBuf, _tetherCtx, out retError); break;
                        case TetherWord.StatusOfCards: r = Ext_GetStatusOfCards(_capBuf, _tetherCtx, out retError); break;
                        default: r = Ext_GetBatteryInfo(_capBuf, _tetherCtx, out retError); break;
                    }
                    rawHead = BitConverter.ToString(_capBacking, 0, TETHER_WORD_SCRATCH_BYTES);
                    if (r != LMX_BOOL_TRUE) { return false; }
                    value = (uint)(_capBacking[0] | (_capBacking[1] << 8) | (_capBacking[2] << 16) | (_capBacking[3] << 24));
                    return true;
                } catch (Exception) {
                    return false;   // an undocumented export that is absent or differently shaped must not be fatal
                }
            }
        }

        // 32 rather than 16: the battery struct runs to at least offset 14, so a wider window is what shows
        // whether more fields follow the ones already identified.
        private const int TETHER_WORD_SCRATCH_BYTES = 32;

        // ===================== Extended connection (raw buffers + held context) =====================
        /// <summary>Enumerate cameras via the extended DLL. Returns model names (index-aligned).</summary>
        public static string[] Ext_Enumerate(out uint retError) {
            lock (_sdkGate) {
                EnsureTetherBuffers();
                byte r = Ext_GetPnPDeviceInfo(_devBuf, out retError);
                int count = Marshal.ReadInt32(_devBuf, 0);
                if (r != LMX_BOOL_TRUE || count <= 0) return Array.Empty<string>();
                var names = new string[count];
                for (int i = 0; i < count; i++)
                    names[i] = Marshal.PtrToStringUni(_devBuf + EXT_DEVINFO_BASE + i * EXT_DEVINFO_STRIDE + EXT_MODELNAME_OFF) ?? "";
                return names;
            }
        }

        /// <summary>Select+open the device by index (7-arg select fills the context; then OpenSession).</summary>
        public static bool Ext_Connect(uint index, out uint retError) {
            lock (_sdkGate) {
                EnsureTetherBuffers();
                // (re)enumerate so the device buffer + global list are fresh, then select this index's dev_Index
                Ext_GetPnPDeviceInfo(_devBuf, out retError);
                int devIndex = Marshal.ReadInt32(_devBuf, EXT_DEVINFO_BASE + (int)index * EXT_DEVINFO_STRIDE);
                byte r = Ext_SelectPnPDevice((uint)devIndex, _devBuf, _tetherCtx, 0, IntPtr.Zero, 0, out retError);
                if (r != LMX_BOOL_TRUE) return false;
                Ext_OpenSession(0x00010001, out _, _tetherCtx, out retError);
                return true;
            }
        }

        public static void Ext_Disconnect(out uint retError) {
            lock (_sdkGate) {
                Ext_CloseSession(_tetherCtx, out retError);
                Ext_CloseDevice(_tetherCtx, out retError);
            }
        }

        // Split accessors so the driver can log/step through the teardown and see which call stalls/crashes.
        public static byte Ext_CloseSessionOnly(out uint retError) {
            lock (_sdkGate) { return Ext_CloseSession(_tetherCtx, out retError); }
        }
        public static byte Ext_CloseDeviceOnly(out uint retError) {
            lock (_sdkGate) { return Ext_CloseDevice(_tetherCtx, out retError); }
        }

        /// <summary>Refresh the SS range so BULB (0xFFFFFFFF) sticks instead of clamping to 60s. Extended only.</summary>
        public static bool Ext_EnsureBulb(out uint retError) {
            lock (_sdkGate) {
                EnsureTetherBuffers();
                for (int attempt = 0; attempt < 5; attempt++) {
                    Ext_SS_GetCapability(_capBuf, _tetherCtx, out _);
                    System.Threading.Thread.Sleep(250);
                    Ext_SS_SetParam(unchecked((uint)0xFFFFFFFF), _tetherCtx, out retError);
                    System.Threading.Thread.Sleep(150);
                    Ext_SS_GetParam(out int cur, _tetherCtx, out _);
                    if ((uint)cur == 0xFFFFFFFF) return true;
                }
                retError = 0; return false;
            }
        }
    }
}
