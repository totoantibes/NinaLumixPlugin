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
        private static byte[] _ctxBacking;
        private static GCHandle _ctxPin;
        private static IntPtr _tetherCtx = IntPtr.Zero;

        private static byte[] _capBacking;
        private static GCHandle _capPin;
        private static IntPtr _capBuf = IntPtr.Zero;   // large scratch for *_GetCapability (avoids OOB)

        private static byte[] _devBacking;
        private static GCHandle _devPin;
        private static IntPtr _devBuf = IntPtr.Zero;    // extended device-info buffer (array-max 64 layout)

        private static void EnsureTetherBuffers() {
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
        private static extern byte Ext_CloseDevice(out uint retError);

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
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_Get_Object_DataSize", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_Get_Object_DataSize(uint objHandle, out uint pDataSize, IntPtr ctx, out uint retError);
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
        public static byte LMX_func_api_SS_Get_Param(out int pulParam, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_SS_GetParam(out pulParam, _tetherCtx, out retError) : Pub_SS_Get_Param(out pulParam, out retError);
        public static byte LMX_func_api_SS_Set_Param(long ulParam, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_SS_SetParam(ulParam, _tetherCtx, out retError) : Pub_SS_Set_Param(ulParam, out retError);
        public static byte LMX_func_api_ISO_Get_Param(out uint pulParam, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_ISO_GetParam(out pulParam, _tetherCtx, out retError) : Pub_ISO_Get_Param(out pulParam, out retError);
        public static byte LMX_func_api_ISO_Set_Param(uint ulParam, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_ISO_SetParam(ulParam, _tetherCtx, out retError) : Pub_ISO_Set_Param(ulParam, out retError);
        public static byte LMX_func_api_Rec_Ctrl_Release(ref LMX_STRUCT_REC_CTRL lpRecCtrl, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_Rec_Ctrl_Release(ref lpRecCtrl, _tetherCtx, out retError) : Pub_Rec_Ctrl_Release(ref lpRecCtrl, out retError);
        public static byte LMX_func_api_Get_Object(uint objHandle, ref byte lpStoreBuf, uint storeBufSize, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_Get_Object(objHandle, ref lpStoreBuf, storeBufSize, _tetherCtx, out retError) : Pub_Get_Object(objHandle, ref lpStoreBuf, storeBufSize, out retError);
        public static byte LMX_func_api_Get_Object_FormatType(uint objHandle, out uint pFormatType, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_Get_Object_FormatType(objHandle, out pFormatType, _tetherCtx, out retError) : Pub_Get_Object_FormatType(objHandle, out pFormatType, out retError);
        public static byte LMX_func_api_Get_Object_DataSize(uint objHandle, out uint pDataSize, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_Get_Object_DataSize(objHandle, out pDataSize, _tetherCtx, out retError) : Pub_Get_Object_DataSize(objHandle, out pDataSize, out retError);
        public static byte LMX_func_api_Get_Object_FileName(uint objHandle, ref LMX_STRUCT_PTP_ARRAY_STRING pFileName, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_Get_Object_FileName(objHandle, ref pFileName, _tetherCtx, out retError) : Pub_Get_Object_FileName(objHandle, ref pFileName, out retError);
        public static byte LMX_func_api_Skip_Object_Transfer(uint objHandle, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_Skip_Object_Transfer(objHandle, _tetherCtx, out retError) : Pub_Skip_Object_Transfer(objHandle, out retError);
        public static byte LMX_func_api_Ctrl_LiveView_Start(out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_Ctrl_LiveView_Start(_tetherCtx, out retError) : Pub_Ctrl_LiveView_Start(out retError);
        public static byte LMX_func_api_Ctrl_LiveView_Stop(out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_Ctrl_LiveView_Stop(_tetherCtx, out retError) : Pub_Ctrl_LiveView_Stop(out retError);

        public static byte LMX_func_api_Get_LiveView_data(
            ref LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM pHistgram, out uint pHistgramSize,
            ref LMX_STRUCT_LIVEVIEW_INFO_POSTURE pPosture, out uint pPostureSize,
            ref LMX_STRUCT_LIVEVIEW_INFO_LEVEL pLevel, out uint pLevelSize,
            ref byte pJpeg, out uint pJpegSize, out uint retError) =>
            NativeBinding.ExtendedMode
                ? Ext_Get_LiveView_data(ref pHistgram, out pHistgramSize, ref pPosture, out pPostureSize, ref pLevel, out pLevelSize, ref pJpeg, out pJpegSize, _tetherCtx, out retError)
                : Pub_Get_LiveView_data(ref pHistgram, out pHistgramSize, ref pPosture, out pPostureSize, ref pLevel, out pLevelSize, ref pJpeg, out pJpegSize, out retError);

        // Capability getters: extended path writes into a large scratch buffer, then copies the leading
        // portion (matching layout: NumOfVal@0, SupportVal@4...) into the managed struct — avoids OOB.
        public static byte LMX_func_api_SS_Get_Capability(ref LMX_STRUCT_SS_CAPA_INFO pInfo, out uint retError) {
            if (!NativeBinding.ExtendedMode) return Pub_SS_Get_Capability(ref pInfo, out retError);
            EnsureTetherBuffers();
            byte r = Ext_SS_GetCapability(_capBuf, _tetherCtx, out retError);
            pInfo = Marshal.PtrToStructure<LMX_STRUCT_SS_CAPA_INFO>(_capBuf);
            return r;
        }
        public static byte LMX_func_api_ISO_Get_Capability(ref LMX_STRUCT_ISO_CAPA_INFO pInfo, out uint retError) {
            if (!NativeBinding.ExtendedMode) return Pub_ISO_Get_Capability(ref pInfo, out retError);
            EnsureTetherBuffers();
            byte r = Ext_ISO_GetCapability(_capBuf, _tetherCtx, out retError);
            pInfo = Marshal.PtrToStructure<LMX_STRUCT_ISO_CAPA_INFO>(_capBuf);
            return r;
        }
        // Dedicated mode-position getter — avoids the CameraMode capability struct whose Tether-buffer layout
        // differs from the managed struct (so reading CurVal_mode_pos from it gives the wrong value).
        public static byte LMX_func_api_CameraMode_Get_Mode_Pos(out uint pulParam, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_CameraMode_GetModePos(out pulParam, _tetherCtx, out retError) : Pub_CameraMode_Get_Mode_Pos(out pulParam, out retError);

        public static byte LMX_func_api_CameraMode_Get_Capability(ref LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO pInfo, out uint retError) {
            if (!NativeBinding.ExtendedMode) return Pub_CameraMode_Get_Capability(ref pInfo, out retError);
            EnsureTetherBuffers();
            byte r = Ext_CameraMode_GetCapability(_capBuf, _tetherCtx, out retError);
            pInfo = Marshal.PtrToStructure<LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO>(_capBuf);
            return r;
        }

        // ===================== Extended-only features (image quality, battery) =====================
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ImageInfo_Set_ImageQuality", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ImageInfo_SetImageQuality(uint quality, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ImageInfo_Get_ImageQuality", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ImageInfo_GetImageQuality(out uint quality, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_ImageInfo_GetCapability", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_ImageInfo_GetCapability(IntPtr buf, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_GetBatteryInfo", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_GetBatteryInfo(out uint info, IntPtr ctx, out uint retError);

        // GH5S image-quality map (may vary per body): 0=Fine, 3=RAW, 4=RAW+Fine.
        public const uint IMGQ_JPEG_FINE = 0, IMGQ_RAW = 3, IMGQ_RAW_JPEG = 4;

        /// <summary>Set still image quality (extended only). Refreshes the range first (like SS/BULB).</summary>
        public static byte Ext_SetImageQuality(uint quality, out uint retError) {
            EnsureTetherBuffers();
            Ext_ImageInfo_GetCapability(_capBuf, _tetherCtx, out _);
            return Ext_ImageInfo_SetImageQuality(quality, _tetherCtx, out retError);
        }
        public static byte Ext_GetImageQuality(out uint quality, out uint retError) =>
            Ext_ImageInfo_GetImageQuality(out quality, _tetherCtx, out retError);
        public static byte Ext_GetBattery(out uint info, out uint retError) =>
            Ext_GetBatteryInfo(out info, _tetherCtx, out retError);

        // ===================== Save destination (SetupFilesConfig target) =====================
        // 0=SD only (default), 1=PC only (cardless), 2=PC+SD. On capture with a PC target the camera fires
        // OBJCT_REQ_TRNSFER with the cardless handle; Get_Object(CARDLESS_TRNSFER_HDL) pulls the frame to the PC.
        public const ushort SAVE_TARGET_SD = 0, SAVE_TARGET_PC = 1, SAVE_TARGET_PC_AND_SD = 2;
        public const uint CARDLESS_TRNSFER_HDL = 0x12345678;   // LMX_DEF_OBJ_CARDLESS_TRNSFER_HDL

        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SetupFilesConfig_Get_Target", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_SetupFilesConfig_GetTarget(out ushort punParam, IntPtr ctx, out uint retError);
        [DllImport(DLLNAME, EntryPoint = "LMX_func_api_SetupFilesConfig_Set_Target", ExactSpelling = true, CallingConvention = cc)]
        private static extern byte Ext_SetupFilesConfig_SetTarget(ushort unParam, IntPtr ctx, out uint retError);

        public static byte LMX_func_api_SetupFilesConfig_Get_Target(out ushort punParam, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_SetupFilesConfig_GetTarget(out punParam, _tetherCtx, out retError) : Pub_SetupFilesConfig_Get_Target(out punParam, out retError);
        public static byte LMX_func_api_SetupFilesConfig_Set_Target(ushort unParam, out uint retError) =>
            NativeBinding.ExtendedMode ? Ext_SetupFilesConfig_SetTarget(unParam, _tetherCtx, out retError) : Pub_SetupFilesConfig_Set_Target(unParam, out retError);

        // ===================== Extended connection (raw buffers + held context) =====================
        /// <summary>Enumerate cameras via the extended DLL. Returns model names (index-aligned).</summary>
        public static string[] Ext_Enumerate(out uint retError) {
            EnsureTetherBuffers();
            byte r = Ext_GetPnPDeviceInfo(_devBuf, out retError);
            int count = Marshal.ReadInt32(_devBuf, 0);
            if (r != LMX_BOOL_TRUE || count <= 0) return Array.Empty<string>();
            var names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = Marshal.PtrToStringUni(_devBuf + EXT_DEVINFO_BASE + i * EXT_DEVINFO_STRIDE + EXT_MODELNAME_OFF) ?? "";
            return names;
        }

        /// <summary>Select+open the device by index (7-arg select fills the context; then OpenSession).</summary>
        public static bool Ext_Connect(uint index, out uint retError) {
            EnsureTetherBuffers();
            // (re)enumerate so the device buffer + global list are fresh, then select this index's dev_Index
            Ext_GetPnPDeviceInfo(_devBuf, out retError);
            int devIndex = Marshal.ReadInt32(_devBuf, EXT_DEVINFO_BASE + (int)index * EXT_DEVINFO_STRIDE);
            byte r = Ext_SelectPnPDevice((uint)devIndex, _devBuf, _tetherCtx, 0, IntPtr.Zero, 0, out retError);
            if (r != LMX_BOOL_TRUE) return false;
            Ext_OpenSession(0x00010001, out _, _tetherCtx, out retError);
            return true;
        }

        public static void Ext_Disconnect(out uint retError) {
            Ext_CloseSession(_tetherCtx, out retError);
            Ext_CloseDevice(out retError);
        }

        /// <summary>Refresh the SS range so BULB (0xFFFFFFFF) sticks instead of clamping to 60s. Extended only.</summary>
        public static bool Ext_EnsureBulb(out uint retError) {
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
