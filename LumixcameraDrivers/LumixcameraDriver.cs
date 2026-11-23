using NINA.Core.Enum;
using NINA.ViewModel;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Model;
using NINA.Image.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using NINA.Profile.Interfaces;
using NINA.Image.ImageData;
using NINA.Profile;
using LumixWrapper;
using static LumixWrapper.LumixCam;
using NINA.Equipment.Equipment.MyGuider;
using Accord.Math;
using NINA.Core.Utility.Notification;
using Google.Protobuf.WellKnownTypes;
using Dasync.Collections;
using Newtonsoft.Json.Linq;
using NINA.Core.Model;
using NINA.Equipment.Utility;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using System.Windows.Media.Imaging;
using System.Reflection;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraDrivers {

    /// <summary>
    /// This Class implements a camera driver plugin instance for the native lumix SDK
    /// </summary>
    public class LumixcameraDriver : BaseINPC, ISettings, ICamera {
        private IProfileService _profileService;
        private readonly IExposureDataFactory _exposureDataFactory;
        private LMX_DEVINFO _lmxDevInfo = new LMX_DEVINFO();
        private LMX_CONNECT_DEVICE_INFO _lmxConnectDeviceInfo = new LMX_CONNECT_DEVICE_INFO();
        private uint retError, deviceConnectVer, returnV;
        private byte ret;
        private uint _index;
        private uint _curIsoValue = 0;
        private int _curSsValue = 0;
        private bool _connected = false;
        private byte[] buffer;
        private uint _lastFormat;   // object format of the most recent capture (RAW vs JPEG) for download decode
        private AsyncObservableCollection<BinningMode> _binningModes;

        private LMX_CALLBACK_FUNC callback;

        private LMX_STRUCT_ISO_CAPA_INFO Iso_CapaInfo = new LMX_STRUCT_ISO_CAPA_INFO();
        private LMX_STRUCT_SS_CAPA_INFO SS_CapaInfo = new LMX_STRUCT_SS_CAPA_INFO();
        private TaskCompletionSource<object> _downloadExposure;
        private CameraSpecs _currentCameraspecs = new CameraSpecs();

        public LumixcameraDriver(IProfileService profileService, IExposureDataFactory exposureDataFactory, LumixCam.LMX_DEVINFO lmxDevInfo, LumixCam.LMX_CONNECT_DEVICE_INFO lmxConnetDeviceInfo, int index) {
            _profileService = profileService;
            _exposureDataFactory = exposureDataFactory;
            _lmxDevInfo = lmxDevInfo;
            _lmxConnectDeviceInfo = lmxConnetDeviceInfo;
            _index = ((uint)index);
            callback = new LMX_CALLBACK_FUNC(NotifyCallbackFunction);
        }

        // Largest object we will accept as a still frame. The biggest Lumix RW2 is well under this (18 MB on
        // the bodies used here, under 100 MB on the highest-resolution ones); a size beyond it is a garbage or
        // uninitialised out-param from an object the camera cannot serve, not a photograph.
        private const uint MAX_OBJECT_BYTES = 256u * 1024u * 1024u;

        // The native SDK calls this from its own thread: it is a reverse P/Invoke, so a managed exception must
        // NEVER leave this method. Unwinding into the SDK's native frame is undefined behaviour and takes the
        // whole NINA process down — Windows reports it as an access violation in the C runtime, with no managed
        // exception text and the log cut off mid-frame (observed 2026-11-23 08:32:57, caused by a full SD card).
        // Catch everything here, fail the frame NINA is waiting on, and let the capture be the casualty instead
        // of the process.
        public int NotifyCallbackFunction(uint cb_event_type, uint cb_event_param) {
            try {
                return DispatchCallbackEvent(cb_event_type, cb_event_param);
            } catch (Exception ex) {
                Logger.Error($"[LumixCapture] handling camera event 0x{cb_event_type:X} (param 0x{cb_event_param:X}) threw; failing this frame: {ex}");
                FailExposure($"The Lumix driver failed while handling camera event 0x{cb_event_type:X}: {ex.Message}");
                return 0;   // event consumed — never let an exception reach native code
            }
        }

        private int DispatchCallbackEvent(uint cb_event_type, uint cb_event_param) {
            switch ((Lmx_event_id)cb_event_type) {
                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE:
                    ExposureFinished(cb_event_param);
                    // Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE?");
                    break;

                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER:
                    // Host/PC transfer request (save target PC-only or PC+SD). cb_event_param is the cardless
                    // handle (0x12345678). A pending transfer MUST be consumed — pulled or skipped — or the
                    // camera keeps a pending-write state and won't power off. If the frame isn't downloaded yet,
                    // pull it to the PC; if it already completed via the card write (PC+SD), release it.
                    if (_downloadExposure != null && !_downloadExposure.Task.IsCompleted) {
                        ExposureFinished(cb_event_param);
                    } else {
                        ReleasePendingTransfer(cb_event_param);
                    }
                    break;

                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_SHUTTER:
                    //Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_SHUTTER");

                    break;

                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD:
                    //Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_OBJCT_ADD");
                    ExposureFinished(cb_event_param);
                    break;

                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_ISO:
                    //Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_ISO");

                    break;

                default:
                    return -1;
            }

            return 0;
        }

        private Dictionary<string, CameraSpecs> lumixCameras;

        // ---- Card capacity ----
        // A full card is invisible from NINA's side: the camera just stops producing frames, and with the save
        // target on PC+SD it produces an empty object instead. These two Tether exports are the only way to see
        // it coming, so the count is read at connect and re-read after a frame arrives empty.
        private uint? _lastKnownCardFreeFrames;
        private uint? _lastKnownCardStatus;
        private bool _recheckCardCapacity;

        // A flat set is 21 frames and a night of subs is hundreds, so fewer than this is worth saying out loud
        // before the session starts rather than discovering at 3 a.m.
        private const uint LOW_CARD_FRAMES = 50;

        private static bool SaveTargetUsesCard(ushort saveTarget) =>
            saveTarget == LumixCam.SAVE_TARGET_SD || saveTarget == LumixCam.SAVE_TARGET_PC_AND_SD;

        // Frames the camera says still fit on the card, or null when it will not say. Both calls are
        // undocumented Tether exports, so the raw bytes are logged either way: this line is what will pin their
        // layout down on a real body, and it is also why they are only ever called from NINA's own thread —
        // never from inside the SDK's capture callback.
        // No SD card holds this many frames, so a bigger number is not a count. Measured: with the slot empty
        // the body answers 0xFFFFFFFF, which read as "room for 4294967295 more frame(s)" until this check
        // existed. With a card in, the same call returned 2576 — a real count, whatever the save target.
        private const uint CARD_FRAMES_IMPLAUSIBLE = 1000000;

        private uint? ReadCardFreeFrames() {
            bool ok = LumixCam.Ext_TryGetFreeSpaceInImages(out uint images, out uint spaceErr, out string spaceRaw);
            bool statusOk = LumixCam.Ext_TryGetStatusOfCards(out uint status, out uint statusErr, out string statusRaw);
            _lastKnownCardStatus = statusOk ? status : (uint?)null;
            if (!ok) {
                Logger.Info($"[LumixCard] the camera did not report card capacity (err={LumixError.Describe(spaceErr)}, bytes={spaceRaw}; status err={LumixError.Describe(statusErr)}, bytes={statusRaw}).");
                _lastKnownCardFreeFrames = null;
                return null;
            }
            if (images == LumixCam.CARD_FRAMES_NO_NUMBER || images >= CARD_FRAMES_IMPLAUSIBLE) {
                Logger.Info($"[LumixCard] the camera gave no usable frame count (0x{images:X8}); card status=0x{status:X} (space bytes={spaceRaw}, status bytes={statusRaw}).");
                _lastKnownCardFreeFrames = null;
                return null;
            }
            Logger.Info($"[LumixCard] room for {images} more frame(s); card status=0x{status:X} (space bytes={spaceRaw}, status bytes={statusRaw}).");
            _lastKnownCardFreeFrames = images;
            return images;
        }

        // Read the card and say so where NINA shows it. Only warns when the save target actually writes to the
        // card — on PC-only the card's state does not matter.
        private void ReportCardCapacity(ushort saveTarget) {
            uint? free = ReadCardFreeFrames();
            if (!SaveTargetUsesCard(saveTarget)) { return; }
            if (free == null) {
                // No usable count AND the save target writes to the card: the camera has somewhere it is meant
                // to write and cannot tell us there is room. The status word separates the two causes — an empty
                // slot reads 0x00000002 and a card in the slot 0x00010002 — so name which one it is, falling
                // back to the vaguer wording when the status word is missing or in some third state.
                bool slotEmpty = _lastKnownCardStatus.HasValue && !LumixCam.CardPresent(_lastKnownCardStatus.Value);
                string reason = slotEmpty
                    ? "there is no card in the camera"
                    : "the camera reports no usable capacity for the card";
                Logger.Warning($"[LumixCard] the save target writes to the SD card, but {reason} — captures will fail until a writable card is in the body or the save target is set to PC only.");
                Notification.ShowWarning($"The save target writes to the camera's SD card, but {reason}. Put in a writable card, or set the plugin's save target to PC only.");
                return;
            }
            if (free.Value == 0) {
                Logger.Error("[LumixCard] the SD card is full and the save target writes to it — captures will fail until the card is cleared or the save target is set to PC only.");
                Notification.ShowError("The camera's SD card is full and the save target writes to it. Clear the card, or set the plugin's save target to PC only.");
            } else if (free.Value < LOW_CARD_FRAMES) {
                Logger.Warning($"[LumixCard] only {free.Value} frame(s) of room left on the SD card.");
                Notification.ShowWarning($"The camera's SD card has room for only {free.Value} more frame(s).");
            }
        }

        // What to tell NINA about the card when a frame arrives empty. Uses the last reading rather than calling
        // the SDK from the capture callback, and asks for a fresh reading on the next exposure.
        private string DescribeCardForFailure() {
            _recheckCardCapacity = true;
            if (_lastKnownCardFreeFrames == 0) {
                return " The SD card was last reported full.";
            }
            if (_lastKnownCardFreeFrames.HasValue) {
                return $" The SD card was last reported as having room for {_lastKnownCardFreeFrames.Value} frame(s).";
            }
            return " If the save target includes the SD card, check that a card is present and not full.";
        }

        // Fail the exposure NINA is awaiting, so a frame that cannot be delivered ends as a failed capture.
        // TrySetException, never TrySetResult: completing the task successfully hands NINA an exposure whose
        // raw bytes are null, because `buffer` was cleared by the previous frame's download.
        private void FailExposure(string reason) {
            var tcs = _downloadExposure;
            if (tcs == null || tcs.Task.IsCompleted) { return; }
            tcs.TrySetException(new Exception(reason));
        }

        // A cardless PC-transfer request must be consumed — pulled or skipped — or the camera keeps a pending
        // write and will not power off. Card objects (OBJCT_ADD) carry no pending transfer to release.
        private void ReleasePendingTransfer(uint cb_event_param) {
            if (cb_event_param != CARDLESS_TRNSFER_HDL) { return; }
            LMX_func_api_Skip_Object_Transfer(cb_event_param, out uint _skipErr);
        }

        private void ExposureFinished(uint cb_event_param) {
            // With PC+SD, one frame fires both OBJCT_REQ_TRNSFER (cardless handle) and OBJCT_ADD (card handle);
            // process only the first so we don't download twice.
            if (_downloadExposure == null || _downloadExposure.Task.IsCompleted) { return; }
            uint formatType;
            uint dataSize;
            LMX_STRUCT_PTP_ARRAY_STRING fileNameArrStr = new LMX_STRUCT_PTP_ARRAY_STRING();

            LMX_func_api_Get_Object_FormatType(cb_event_param, out formatType, out retError);
            LMX_func_api_Get_Object_DataSize(cb_event_param, out dataSize, out retError);
            LMX_func_api_Get_Object_FileName(cb_event_param, ref fileNameArrStr, out retError);
            _lastFormat = formatType;
            Logger.Info($"[LumixCapture] object=0x{cb_event_param:X} format=0x{formatType:X} size={dataSize}");
            if (dataSize == 0) {
                // The camera raised a transfer event for an object that holds no data. Seen when the SD card is
                // full and the save target includes SD: the card write has nowhere to go, so the frame never
                // exists. This used to call TrySetResult(null) — completing the exposure as a SUCCESS with no
                // data — which handed NINA an exposure whose raw bytes were null. Fail the frame instead.
                Logger.Warning("[LumixCapture] object data size is 0 — nothing to download; failing this frame. If the save target includes the SD card, check that the card is not full.");
                ReleasePendingTransfer(cb_event_param);
                FailExposure("The camera reported a frame with no data." + DescribeCardForFailure());
                return;
            }
            if (dataSize > MAX_OBJECT_BYTES) {
                // Allocating a garbage size would throw OutOfMemory inside this native callback — the one place
                // an exception must not be raised. Refuse the object instead of trying to hold it.
                Logger.Warning($"[LumixCapture] object size {dataSize} exceeds the {MAX_OBJECT_BYTES} byte limit for a still frame — refusing to allocate; failing this frame.");
                ReleasePendingTransfer(cb_event_param);
                FailExposure($"The camera reported an implausible frame size of {dataSize} bytes.");
                return;
            }
            buffer = new byte[dataSize];
            byte gr = 0;
            try { gr = LMX_func_api_Get_Object(cb_event_param, ref buffer[0], dataSize, out retError); } catch (Exception ex) { Logger.Error("[LumixCapture] Get_Object threw: " + ex); }
            if (dataSize >= 4) {
                Logger.Info($"[LumixCapture] Get_Object ret={gr} err={retError}; magic={buffer[0]:X2} {buffer[1]:X2} {buffer[2]:X2} {buffer[3]:X2}");
            }

            // Guard against a bogus object. On the first frame of a session the camera can fire an OBJCT event
            // for a placeholder/association object whose Get_Object fails (ret=0) and returns a zero-filled buffer
            // (observed as format 0x3, 128 KB). Forwarding that to NINA makes LibRaw throw "not a RAW file". Only
            // complete the download once we've actually pulled a recognisable image; otherwise ignore this object
            // and keep awaiting the real frame — the valid RW2/JPEG arrives on the next OBJCT event.
            if (gr != LMX_BOOL_TRUE || !IsKnownImageHeader(buffer, dataSize)) {
                Logger.Warning($"[LumixCapture] object 0x{cb_event_param:X} is not a usable image (ret={gr}, format=0x{formatType:X}, size={dataSize}) — ignoring, waiting for the real frame.");
                ReleasePendingTransfer(cb_event_param);
                return;   // do NOT complete the exposure with junk data
            }

            _downloadExposure.TrySetResult(null);
            return;
        }

        // A recognisable still-image header: Panasonic RW2 ("IIU\0"), TIFF (RW2 is TIFF-based), or JPEG.
        private static bool IsKnownImageHeader(byte[] b, uint size) {
            if (b == null || size < 4) { return false; }
            if (b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x55 && b[3] == 0x00) { return true; } // IIU\0  RW2
            if (b[0] == 0x49 && b[1] == 0x49 && b[2] == 0x2A && b[3] == 0x00) { return true; } // II*\0  TIFF LE
            if (b[0] == 0x4D && b[1] == 0x4D && b[2] == 0x00 && b[3] == 0x2A) { return true; } // MM\0*  TIFF BE
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) { return true; }                 // JPEG
            return false;
        }

        // ---- Extended-mode helpers: manual-mode whitelist + shutter-speed snapping ----

        private ushort _lastNotifiedModePos = 0xFFFF;
        private bool _warnedJpeg = false;   // warn once per connect if the camera is capturing JPEG, not RAW
        private bool _warnedLibRaw = false; // warn once per connect if the built-in LibRaw decoder failed and we fell back
        private List<(double seconds, int raw)> _ssTable;

        private static bool IsManualMode(ushort modePos) =>
            modePos == (ushort)Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_M;

        // C1-C3 custom presets. The Tether SDK reports these dial positions but NOT the base program (M/P/A/S)
        // the preset encodes, so we can't be sure a custom set is Manual. We give the user the benefit of the
        // doubt: assume it is M-based, proceed, and only warn.
        private static bool IsCustomMode(ushort modePos) =>
            modePos == (ushort)Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM
            || modePos == (ushort)Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM2
            || modePos == (ushort)Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM3;

        // Warn (never block) about the current exposure mode. M is silent; a C1-C3 custom preset gets a soft
        // "assumed M-based" warning; anything else gets the stronger warning. Change-detected so repeated
        // captures in the same mode don't spam the same notification.
        private void CheckExposureMode(ushort modePos) {
            if (IsManualMode(modePos)) { _lastNotifiedModePos = modePos; return; }
            if (modePos == _lastNotifiedModePos) { return; }
            _lastNotifiedModePos = modePos;
            if (IsCustomMode(modePos)) {
                Notification.ShowWarning("Camera is in a custom mode (C1-C3), assumed to be an M-based preset. Proceeding - set the preset to Manual if exposures look wrong.");
            } else {
                Notification.ShowWarning("Camera is not in M or a custom (C1-C3) preset - exposures may be incorrect.");
            }
        }

        // Decode the camera's supported shutter-speed list into (seconds, raw) once. Whole-second speeds
        // carry 0x80000000 (seconds = (v & 0x7fffffff)/1000); fractional speeds are 1/x (seconds = 1000/v).
        // BULB / AUTO / UNKNOWN are excluded.
        private void BuildSsTable() {
            _ssTable = new List<(double, int)>();
            if (_exposures == null) { _exposures = new List<int>(); }
            var support = SS_CapaInfo.Capa_Enum.SupportVal;
            if (support == null) { return; }
            // Iterate only the valid entries (NumOfVal), not the full fixed-size array — the tail is garbage.
            int count = SS_CapaInfo.Capa_Enum.NumOfVal;
            if (count <= 0 || count > support.Length) { count = support.Length; }
            for (int i = 0; i < count; i++) {
                uint v = (uint)support[i];
                if (v == 0) { continue; }
                if (v == 0xFFFFFFFF) { continue; }                                                                  // BULB
                if (v == (uint)Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_UNKNOWN) { continue; }
                if (v == (uint)Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_AUTO) { continue; }
                double sec = ((v & 0x80000000) != 0) ? (double)(v & 0x7fffffff) / 1000.0 : 1000.0 / v;
                if (sec <= 0) { continue; }
                _ssTable.Add((sec, unchecked((int)v)));
                if ((v & 0x80000000) != 0) { _exposures.Add((int)(v & 0x7fffffff) / 1000); }   // whole-second fallback list
            }
        }

        // Nearest supported shutter speed (raw value) to the requested seconds; 0 if the list is unknown.
        private int NearestSsRaw(double requestedSeconds, out double actualSeconds) {
            if (_ssTable == null || _ssTable.Count == 0) { BuildSsTable(); }
            actualSeconds = requestedSeconds;
            if (_ssTable == null || _ssTable.Count == 0) { return 0; }
            var best = _ssTable[0];
            double bestErr = Math.Abs(best.seconds - requestedSeconds);
            foreach (var e in _ssTable) {
                double err = Math.Abs(e.seconds - requestedSeconds);
                if (err < bestErr) { bestErr = err; best = e; }
            }
            actualSeconds = best.seconds;
            return best.raw;
        }

        public bool HasShutter => true;

        public double Temperature {
            get => double.NaN;
        }

        public double TemperatureSetPoint {
            get => double.NaN;

            set {
            }
        }

        public short BinX { get => 1; set => throw new NotImplementedException(); }
        public short BinY { get => 1; set => throw new NotImplementedException(); }

        public string SensorName {
            get {
                if (Connected) {
                    return _lmxDevInfo.dev_ModelName;
                } else {
                    return string.Empty;
                }
            }
        }

        public SensorType SensorType { get => SensorType.RGGB; set => throw new NotImplementedException(); }

        public short BayerOffsetX { get => 0; set => throw new NotImplementedException(); }

        public short BayerOffsetY { get => 0; set => throw new NotImplementedException(); }

        public int CameraXSize {
            get {
                if (Connected) {
                    return _currentCameraspecs.Width;
                } else {
                    return 0;
                }
            }
        }

        public int CameraYSize {
            get {
                if (Connected) {
                    return _currentCameraspecs.Height;
                } else {
                    return 0;
                }
            }
        }

        private IList<int> _exposures;

        // Exposure bounds are derived from the supported-shutter LIST (reliable in both DLLs), not from
        // CurVal_Range — the Tether capability buffer's range fields sit at a different offset than the public
        // struct, so reading them via PtrToStructure yielded a bogus ExposureMin that clamped NINA (e.g. the
        // Flat Wizard) to 1s and never let sub-second exposures through.
        public double ExposureMin {
            get {
                if (!Connected) { return 0; }
                if (_ssTable == null || _ssTable.Count == 0) { BuildSsTable(); }
                double min = (_ssTable != null && _ssTable.Count > 0) ? _ssTable.Min(e => e.seconds) : 0;
                Logger.Debug($"[LumixSS] ExposureMin={min:0.######}s (from {(_ssTable?.Count ?? 0)} speeds)");
                return min;
            }
        }

        public double ExposureMax {
            get {
                if (!Connected) { return 0; }
                if (_ssTable == null || _ssTable.Count == 0) { BuildSsTable(); }
                double listMax = (_ssTable != null && _ssTable.Count > 0) ? _ssTable.Max(e => e.seconds) : 60;
                // Extended mode supports Bulb (>60s) via the open/hold/close shutter sequence, so allow long
                // exposures (cap at 1 hour). Standard mode is limited to the discrete list (<=60s).
                return NativeBinding.ExtendedMode ? Math.Max(listMax, 3600) : listMax;
            }
        }

        public short MaxBinX { get => 1; set => throw new NotImplementedException(); }

        public short MaxBinY { get => 1; set => throw new NotImplementedException(); }

        public double PixelSizeX {
            get {
                if (Connected) {
                    return _currentCameraspecs.PixelPitch;
                } else {
                    return 0;
                }
            }
        }

        public double PixelSizeY {
            get {
                if (Connected) {
                    return _currentCameraspecs.PixelPitch;
                } else {
                    return 0;
                }
            }
        }

        public bool CanSetTemperature => false;

        public bool CoolerOn {
            get => false;
            set {
            }
        }

        public double CoolerPower => double.NaN;

        public bool HasDewHeater => false;

        public bool DewHeaterOn {
            get => false;
            set {
            }
        }

        public CameraStates CameraState => CameraStates.NoState; // TODO - lumix does not give info...

        public bool CanSubSample => false;

        public bool EnableSubSample { get; set; }

        public int SubSampleX { get; set; }

        public int SubSampleY { get; set; }

        public int SubSampleWidth { get; set; }

        public int SubSampleHeight { get; set; }

        public bool CanShowLiveView => true; //TO DO test logic

        private bool _liveViewEnabled;

        public bool LiveViewEnabled {
            get => _liveViewEnabled;
            set {
                _liveViewEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool HasBattery => true;

        // ---- Battery ----
        // NINA polls BatteryLevel on its device-update cycle, roughly every two seconds, and this body's
        // property reads are not free — that cycle has already been seen overrunning its own interval. So the
        // reading is cached and refreshed at most once every BATTERY_CACHE_SECONDS.
        private const int BATTERY_CACHE_SECONDS = 20;
        private int _batteryPercent = -1;
        private DateTime _batteryReadAt = DateTime.MinValue;
        private uint _lastLoggedBatteryWord = uint.MaxValue;

        // -1 is NINA's "unknown" — what every camera without a battery reports, and what we report rather than
        // show a number we cannot justify. It used to be a hardcoded 100.
        public int BatteryLevel {
            get {
                if (!Connected || !NativeBinding.ExtendedMode) { return -1; }
                // Never read the battery while live view is streaming. NINA calls metaData.FromCamera(this) for
                // every live-view frame, so this getter sits in that loop, and before this property did any work
                // it was a hardcoded 100 — no native call at all. On 2026-11-23 at 16:15:33 a refresh that fell
                // due mid-live-view came back having written nothing, the next live-view poll produced no frame,
                // and the process died 1.1 s later. Cause unproven, but a property read competing with the
                // live-view stream has no business being in that path: serve the cached value instead.
                if (LiveViewEnabled) { return _batteryPercent; }
                if ((DateTime.UtcNow - _batteryReadAt).TotalSeconds < BATTERY_CACHE_SECONDS) { return _batteryPercent; }
                _batteryReadAt = DateTime.UtcNow;
                _batteryPercent = ReadBatteryPercent();
                return _batteryPercent;
            }
        }

        private int ReadBatteryPercent() {
            if (!LumixCam.Ext_TryGetBatteryInfo(out uint level, out uint levelAlt, out uint head, out uint err, out string rawHead)) {
                Logger.Info($"[LumixBattery] the camera did not report battery info (err={LumixError.Describe(err)}, bytes={rawHead}).");
                return -1;
            }
            if (level != _lastLoggedBatteryWord) {
                _lastLoggedBatteryWord = level;
                // Both candidates are logged: offset 8 is the one used, offset 12 held the same 100 on the body
                // this was measured on, and only a reading at some other level can rule it out.
                Logger.Info($"[LumixBattery] level={level} (offset 8), alt={levelAlt} (offset 12), head=0x{head:X8}, bytes={rawHead}.");
            }
            // Zero is treated as unknown, not as an empty battery: a call that returns success without writing
            // leaves the scratch zeroed, and a genuinely flat battery would have switched the body off rather
            // than answered. Anything outside 1-100 cannot honestly be shown as a percentage, so it stays
            // unknown — and the line above keeps the raw struct in the log either way.
            if (level >= 1 && level <= 100) { return (int)level; }
            return -1;
        }

        public int BitDepth {
            get {
                if (Properties.Settings.Default.BitDepth != 0) {
                    return Properties.Settings.Default.BitDepth;
                } else {
                    return 14;
                }
            }
        }

        public bool CanSetOffset => false;

        public int Offset { get => -1; set => throw new NotImplementedException(); }

        public int OffsetMin => 0;

        public int OffsetMax => 0;

        public bool CanSetUSBLimit => false;

        public int USBLimit { get => -1; set => throw new NotImplementedException(); }

        public int USBLimitMin => -1;

        public int USBLimitMax => -1;

        public int USBLimitStep => -1;

        public bool CanGetGain {
            get {
                if (Connected) {
                    return true;
                } else { return false; }
            }
        }

        public bool CanSetGain => CanGetGain;

        // Value mode (matches NINA's own DSLR/ISO cameras): Gain is the ISO VALUE, Gains is the list of ISO
        // values, GainMin/GainMax are the min/max ISO.
        public int GainMax => (Connected && Gains != null && Gains.Count > 0) ? Gains.Max() : 0;

        public int GainMin => (Connected && Gains != null && Gains.Count > 0) ? Gains.Min() : 0;

        // Gain is the ISO VALUE (matches NINA's DSLR cameras). The setter maps the value back to the camera's
        // original raw ISO (preserving extended-ISO markers) so it sends exactly what the camera reported.
        public int Gain {
            get {
                return (int)(Iso_CapaInfo.CurVal & 0x0FFFFFFF);
            }
            set {
                _ = Gains;
                uint raw = (uint)value;
                if (_gains != null && _gainsRaw != null) {
                    int idx = _gains.IndexOf(value);
                    if (idx >= 0) { raw = _gainsRaw[idx]; }
                }
                ret = LMX_func_api_ISO_Set_Param(raw, out retError);
            }
        }

        public double ElectronsPerADU => double.NaN;

        public IList<string> ReadoutModes {
            get {
                List<string> readoutModes = new List<string> { "Default" };
                return readoutModes;
            }
        }

        public short ReadoutMode {
            get => 0;
            set { }
        }

        public short ReadoutModeForSnapImages { get => 0; set { } }
        public short ReadoutModeForNormalImages { get => 0; set { } }

        private IList<int> _gains;
        private List<uint> _gainsRaw;   // original raw ISO values aligned with _gains (for the setter)

        public IList<int> Gains {
            get {
                if (_gains == null) {
                    _gains = new List<int>();
                    _gainsRaw = new List<uint>();
                    // Iterate only the valid entries (NumOfVal), not the fixed 512-slot array — the tail is
                    // zeros and was flooding the gain list (also broke the old plugin). Skip the Auto/i-ISO/
                    // Unknown sentinels; the display ISO is the masked value (0x20019000 -> 102400), but keep the
                    // ORIGINAL raw value (with markers) so the setter sends exactly what the camera reported.
                    var isoSupport = Iso_CapaInfo.Capa_Enum.SupportVal;
                    if (isoSupport != null) {
                        int isoCount = Iso_CapaInfo.Capa_Enum.NumOfVal;
                        if (isoCount <= 0 || isoCount > isoSupport.Length) { isoCount = isoSupport.Length; }
                        var pairs = new List<KeyValuePair<int, uint>>();
                        for (int i = 0; i < isoCount; i++) {
                            uint val = (uint)isoSupport[i];
                            if (val == (uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_AUTO) { continue; }
                            if (val == (uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_I_ISO) { continue; }
                            if (val == (uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_UNKNOWN) { continue; }
                            int iso = (int)(val & 0x0FFFFFFF);
                            if (iso > 0) { pairs.Add(new KeyValuePair<int, uint>(iso, val)); }
                        }
                        pairs.Sort((a, b) => a.Key.CompareTo(b.Key));
                        foreach (var p in pairs) { _gains.Add(p.Key); _gainsRaw.Add(p.Value); }
                    }
                }

                return _gains;
            }
        }

        public AsyncObservableCollection<BinningMode> BinningModes {
            get {
                if (_binningModes == null) {
                    _binningModes = new AsyncObservableCollection<BinningMode>();
                    _binningModes.Add(new BinningMode(1, 1));
                }

                return _binningModes;
            }
        }

        public bool HasSetupDialog => false;

        public string Id => "Lumix";

        // Return the enumerated model name even before connecting, so the camera shows up (named) in NINA's
        // camera picker — the driver isn't Connected yet when the chooser renders it.
        public string Name {
            get {
                var n = _lmxDevInfo.dev_ModelName;
                return string.IsNullOrWhiteSpace(n) ? "Lumix Camera" : n;
            }
        }

        public string DisplayName => Name;

        public string Category { get => "Lumix"; }

        public bool Connected {
            get {
                return _connected;
            }
        }

        private Assembly assembly = Assembly.GetExecutingAssembly();

        public string Description => ((AssemblyDescriptionAttribute)assembly.GetCustomAttribute(typeof(AssemblyDescriptionAttribute))).Description;

        public string DriverInfo => ((AssemblyTitleAttribute)assembly.GetCustomAttribute(typeof(AssemblyTitleAttribute))).Title;

        public string DriverVersion => ((AssemblyFileVersionAttribute)assembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute))).Version;

        public IList<string> SupportedActions => new List<string>();

        public void SendCommandBlind(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public bool SendCommandBool(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public string SendCommandString(string command, bool raw = true) {
            throw new NotImplementedException();
        }

        public void SetupDialog() {
            throw new NotImplementedException();
        }

        public string Action(string actionName, string actionParameters) {
            throw new NotImplementedException();
        }

        public void SetBinning(short x, short y) {
            // ignore throw new NotImplementedException();
        }

        // this is for nightly 3.2.067
        public void UpdateSubSampleArea() {
            throw new NotImplementedException();
        }

        private CancellationTokenSource bulbCompletionCTS = null;

        private Task bulbCompletionTask = null;

        public void StartExposure(CaptureSequence sequence) {
            if (Connected) {
                if (_recheckCardCapacity) {
                    // The previous frame came back empty. Re-read the card here, on NINA's thread, so the next
                    // failure message carries a current number instead of the one from connect.
                    _recheckCardCapacity = false;
                    ReportCardCapacity((ushort)Properties.Settings.Default.SaveTarget);
                }
                if (_downloadExposure != null && _downloadExposure.Task.Status <= TaskStatus.Running) {
                    Notification.ShowWarning("Another exposure still in progress. Cancelling it to start another.");
                    Logger.Warning("An exposure was still in progress. Cancelling it to start another.");
                    try { bulbCompletionCTS?.Cancel(); } catch { }
                    _downloadExposure.TrySetCanceled();
                }
                LMX_STRUCT_REC_CTRL lmx_rec_ctrl = new LMX_STRUCT_REC_CTRL();
                lmx_rec_ctrl.CtrlID = ((uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_ONESHOT);
                lmx_rec_ctrl.ParamData.NumOfVal = 0;
                double exposureTime = sequence.ExposureTime;

                // Re-check the exposure mode every capture so a mid-session dial change out of a manual mode is
                // caught (the SDK exposes no mode-change push event, so we poll). Warn once per change to avoid
                // spam. Runs on both DLLs; the Tether DLL additionally recognises the C1-C3 custom presets.
                if (LMX_func_api_CameraMode_Get_Mode_Pos(out uint modePosNow, out uint cmErr) == LMX_BOOL_TRUE) {
                    CheckExposureMode((ushort)modePosNow);
                }

                Logger.Debug("Prepare start of exposure: " + sequence);
                // RunContinuationsAsynchronously is essential: the completion (TrySetResult) happens on the
                // native DLL callback thread, and without this NINA's continuation (LibRaw decode of the frame)
                // would run inline on that native thread and hang the camera pipeline — badly for long bulb subs.
                _downloadExposure = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (_exposures == null) { _ = ExposureMin; }   // lazily build the discrete exposure list (was NRE on 1st shot)

                // Extended mode: use BULB for ANY exposure longer than 1s so arbitrary / unlisted durations
                // (e.g. 7s, or >60s) are timed exactly, instead of snapping to the nearest discrete shutter
                // speed. Sub-second (<=1s) still uses the discrete speeds (bulb has a ~1s practical floor).
                // SS=BULB, open shutter (0x12), hold, then close (0x13) + finalize (0x19). Completion arrives
                // via OBJCT_ADD like a normal frame; the close runs on a cancellable timer so Stop/Abort ends
                // it early.
                if (NativeBinding.ExtendedMode && exposureTime > 1.0) {
                    if (!LumixCam.Ext_EnsureBulb(out retError)) {
                        Notification.ShowWarning("Could not engage BULB mode for the exposure.");
                        _downloadExposure.TrySetCanceled();
                        return;
                    }
                    var bulbOpen = new LMX_STRUCT_REC_CTRL { CtrlID = (uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_BULB_START };
                    bulbOpen.ParamData.NumOfVal = 0;
                    byte bo = LMX_func_api_Rec_Ctrl_Release(ref bulbOpen, out retError);
                    Logger.Info($"[LumixBulb] open (0x12) ret={bo} err={retError}; holding {exposureTime}s");
                    try { bulbCompletionCTS?.Cancel(); } catch { }
                    bulbCompletionCTS = new CancellationTokenSource();
                    var bulbToken = bulbCompletionCTS.Token;
                    bulbCompletionTask = Task.Run(async () => {
                        try { await CoreUtil.Wait(TimeSpan.FromSeconds(exposureTime), bulbToken); } catch { }
                        try {
                            var bulbClose = new LMX_STRUCT_REC_CTRL { CtrlID = (uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_BULB_STOP };
                            bulbClose.ParamData.NumOfVal = 0;
                            byte cr = LMX_func_api_Rec_Ctrl_Release(ref bulbClose, out uint ce);
                            var bulbFin = new LMX_STRUCT_REC_CTRL { CtrlID = (uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_BULB_FINALIZE };
                            bulbFin.ParamData.NumOfVal = 0;
                            byte fr = LMX_func_api_Rec_Ctrl_Release(ref bulbFin, out uint fe);
                            Logger.Info($"[LumixBulb] close (0x13 ret={cr} err={ce}) + finalize (0x19 ret={fr} err={fe}) done; awaiting image.");
                        } catch (Exception bex) {
                            Logger.Error("[LumixBulb] close/finalize threw: " + bex);
                            try { _downloadExposure?.TrySetException(bex); } catch { }
                        }
                    });
                    return;
                }
                if (exposureTime > 60.0) {
                    // Standard mode has no BULB, so it cannot exceed the 60s discrete-list maximum.
                    Notification.ShowWarning("Exposures over 60s need extended (LUMIX Tether) mode. Standard mode is capped at 60s.");
                    _downloadExposure.TrySetCanceled();
                    return;
                }

                // The camera accepts only discrete shutter speeds. NINA (e.g. the Flat Wizard's dichotomy) asks
                // for arbitrary times; snap to the nearest supported speed rather than failing and silently
                // reusing the previous value, which broke exposure-time search. Works on both the public and
                // Tether DLLs (both expose the supported-speed list and the setter). [needs on-camera verify]
                int rawSs = NearestSsRaw(exposureTime, out double actualSec);
                Logger.Info($"[LumixSS] req={exposureTime:0.#####}s ssTable={(_ssTable?.Count ?? -1)} nearest={actualSec:0.#####}s raw=0x{(uint)rawSs:X8}");
                if (rawSs != 0) {
                    if (Math.Abs(actualSec - exposureTime) > 1e-6) {
                        Logger.Info($"Requested {exposureTime:0.####}s is not a supported shutter speed; using nearest {actualSec:0.####}s.");
                    }
                    byte setR = LMX_func_api_SS_Set_Param(rawSs, out uint setErr);
                    LMX_func_api_SS_Get_Param(out int rbVal, out uint rbErr);
                    Logger.Info($"[LumixSS] set raw=0x{(uint)rawSs:X8} ret={setR} err={setErr}; readback=0x{(uint)rbVal:X8}");
                } else if (exposureTime >= 1 && _exposures.Contains((int)exposureTime)) {
                    // Fallback (capability table empty): original exact-match path.
                    LMX_func_api_SS_Set_Param((((int)(exposureTime) * 1000) | 0x80000000), out uint retError);
                } else if (exposureTime < 1) {
                    LMX_func_api_SS_Set_Param((int)(1000 / exposureTime), out uint retError);
                } else {
                    Notification.ShowWarning("Exposure could not be matched to a supported shutter speed; using the last good setting.");
                }
                // for non bulb
                Logger.Debug("Start capture");
                ret = LMX_func_api_Rec_Ctrl_Release(ref lmx_rec_ctrl, out retError);
                Logger.Debug($"Rec_Ctrl_Release ret={ret} retError={retError}");
                if (ret != LMX_BOOL_TRUE) {
                    // Only a genuine failure (function returned not-TRUE). The old code warned whenever
                    // retError==1, which is an error CODE not a bool — that fired a false error on the first shot.
                    // Decoded, because the raw number is what made the 2026-11-23 failure unreadable: err=262147
                    // was LMX_DEF_ERR_COM_DATA_RCV, a transport error - not the body refusing the shot.
                    Logger.Warning($"Single-shot release did not succeed (ret={ret}, err={LumixError.Describe(retError)}).");
                }
            }
        }

        public async Task WaitUntilExposureIsReady(CancellationToken token) {
            using (token.Register(() => AbortExposure())) {
                await _downloadExposure.Task;
            }
        }

        public void StopExposure() {
            // End a bulb exposure early: cancelling the hold timer fires the close (0x13) + finalize (0x19)
            // in the bulb task. For a normal (<=60s) frame there is no timer and nothing to stop.
            try { bulbCompletionCTS?.Cancel(); } catch { }
        }

        public void AbortExposure() {
            if (_downloadExposure != null && _downloadExposure.Task.Status <= TaskStatus.Running) {
                Notification.ShowWarning("Exposure still in progress. Cancelling it.");
                Logger.Warning("Exposure still in progress. Cancelling it.");
                try { bulbCompletionCTS?.Cancel(); } catch { }
                _downloadExposure.TrySetCanceled();
            }
            StopExposure();
        }

        public async Task<IExposureData> DownloadExposure(CancellationToken token) {
            if (_downloadExposure.Task.IsCanceled) { return null; }
            Logger.Debug("Waiting for download of exposure");
            await _downloadExposure.Task;
            Logger.Debug("Downloading of exposure complete. Converting image to internal array");

            try {
                var metaData = new ImageMetaData();
                metaData.FromCamera(this);

                // JPEG path: the camera captured a JPEG (or the user forced it) — decode it directly so NINA
                // can display it even when its RAW converter can't decode this body's .RW2 (e.g. GH7, issue #1).
                bool isJpeg = _lastFormat.IsEqual(Lmx_def_lib_object_format.LMX_DEF_OBJ_FORMAT_JPEG);
                if (isJpeg) {
                    // The camera captured a JPEG. If the user did NOT ask for JPEG, the body is set to a
                    // JPEG quality (not RAW) — warn, because the frame will be shown in grayscale. In standard
                    // mode the plugin cannot change the quality (Tether-only); the user must set RAW on the body.
                    if (!Properties.Settings.Default.PreferJpeg && !_warnedJpeg) {
                        _warnedJpeg = true;
                        Notification.ShowWarning(NativeBinding.ExtendedMode
                            ? "Camera captured JPEG, not RAW. Reconnect — the plugin sets RAW in extended mode."
                            : "Camera captured JPEG, not RAW. Set the camera's image quality to RAW on the body — the plugin can't change it in standard mode.");
                    }
                    Logger.Info($"Captured object is JPEG (PreferJpeg={Properties.Settings.Default.PreferJpeg}) — re-mosaicing to Bayer for colour display.");
                    return DecodeJpegToBayerExposure(buffer, metaData);
                }

                // Optional: decode the RW2 in-plugin with the bundled LibRaw 0.22 (for N.I.N.A. <= 3.2, whose
                // DCRaw/FreeImage converters don't know newer bodies — GH7, S5 II, S9 ...). OFF by default;
                // N.I.N.A. 3.3+ has its own LibRaw. On any failure fall through to N.I.N.A.'s converter.
                if (Properties.Settings.Default.UseBuiltInLibRaw) {
                    bool bitScaling = _profileService.ActiveProfile.CameraSettings.BitScaling;
                    if (LibRawDecoder.TryDecode(buffer, this.BitDepth, bitScaling, out var frame, out var librawError)) {
                        if (frame.Pattern != SensorType.Monochrome && metaData.Camera.BayerPattern != BayerPatternEnum.None) {
                            metaData.Camera.SensorType = frame.Pattern;
                            metaData.Camera.BayerOffsetX = 0;
                            metaData.Camera.BayerOffsetY = 0;
                        }
                        Logger.Info($"[LibRaw] decoded RW2 in-plugin: {frame.Width}x{frame.Height}, pattern={frame.Pattern}, max={frame.MaxPixelValue}, bitDepth={frame.BitDepth} (LibRaw {frame.LibRawVersion}).");
                        return _exposureDataFactory.CreateImageArrayExposureData(
                            input: frame.Pixels,
                            width: frame.Width,
                            height: frame.Height,
                            bitDepth: frame.BitDepth,
                            isBayered: frame.Pattern != SensorType.Monochrome,
                            metaData: metaData);
                    }
                    Logger.Error($"[LibRaw] built-in decode failed, falling back to N.I.N.A.'s RAW converter: {librawError}");
                    if (!_warnedLibRaw) {
                        _warnedLibRaw = true;
                        Notification.ShowWarning("Built-in LibRaw decoder failed (" + librawError + "). Falling back to N.I.N.A.'s RAW converter.");
                    }
                }

                return _exposureDataFactory.CreateRAWExposureData(
                    converter: _profileService.ActiveProfile.CameraSettings.RawConverter,
                    rawBytes: buffer,
                    rawType: "rw2",
                    bitDepth: this.BitDepth,
                    metaData: metaData);
            } finally {
                if (buffer != null) {
                    buffer = null;
                }
            }
        }

        public void StartLiveView(CaptureSequence sequence) {
            LMX_func_api_Ctrl_LiveView_Start(out retError);
            LiveViewEnabled = true;
        }

        // How long to wait before asking the camera for a live-view frame again when it had none ready. NINA's
        // live-view loop has no delay of its own, so without this the poll is a spin.
        private const int LIVEVIEW_EMPTY_POLL_WAIT_MS = 20;

        public Task<IExposureData> DownloadLiveView(CancellationToken token) {
            return Task.Run<IExposureData>(() => {
                // The camera does not always have a frame ready, and `returnedJpegSize` — written by the SDK on
                // every call — was never read, here or in the grayscale path this replaced. The whole fixed-size
                // buffer went to the JPEG decoder, so an empty poll meant decoding a zero-filled array: that
                // throws WINCODEC_ERR_COMPONENTNOTFOUND, and because NINA's loop has no catch of its own the
                // exception ended live view (observed 2026-11-23 16:15:33). Poll until a frame actually arrives.
                byte[] jpegDataPos = new byte[LMX_DEF_LIVEVIEW_STREAMDATA_SIZE_MAX];
                uint returnedJpegSize;
                while (true) {
                    token.ThrowIfCancellationRequested();
                    LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM histgramBuf = new LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM();
                    uint histgramBufSize = LMX_DEF_LIVEVIEW_HISTGRAM_ELEMENT_SIZE;
                    LMX_STRUCT_LIVEVIEW_INFO_POSTURE postureBuf = new LMX_STRUCT_LIVEVIEW_INFO_POSTURE();
                    uint postureBufSize = 32;
                    LMX_STRUCT_LIVEVIEW_INFO_LEVEL levelBuf = new LMX_STRUCT_LIVEVIEW_INFO_LEVEL();
                    uint levelBufSize = 32;// sizeof(LMX_STRUCT_LIVEVIEW_INFO_LEVEL);
                    byte r = LMX_func_api_Get_LiveView_data(ref histgramBuf, out histgramBufSize, ref postureBuf, out postureBufSize, ref levelBuf, out levelBufSize, ref jpegDataPos[0], out returnedJpegSize, out retError);
                    if (r == LMX_BOOL_TRUE && returnedJpegSize > 4 && returnedJpegSize <= jpegDataPos.Length
                        && jpegDataPos[0] == 0xFF && jpegDataPos[1] == 0xD8 && jpegDataPos[2] == 0xFF) {
                        break;
                    }
                    System.Threading.Thread.Sleep(LIVEVIEW_EMPTY_POLL_WAIT_MS);
                }

                // Trim to the bytes the camera actually wrote; the rest of the buffer is stale or zero.
                byte[] frame = new byte[returnedJpegSize];
                Array.Copy(jpegDataPos, frame, (int)returnedJpegSize);

                var metaData = new ImageMetaData();
                metaData.FromCamera(this);
                // The camera's live-view JPEG is already full colour. Re-mosaic it into a synthetic
                // single-plane Bayer (RGGB) array and hand it to NINA as isBayered so NINA's own
                // debayer step (the same one used for real captures) produces a colour preview,
                // instead of flattening the frame to Gray16 as before.
                return DecodeJpegToBayerExposure(frame, metaData);
            });
        }

        /// <summary>
        /// Decodes a JPEG frame that the camera already rendered in full colour (live view, or the
        /// JPEG-capture fallback) and re-mosaics it into a synthetic single-plane Bayer array matching
        /// this driver's declared SensorType (RGGB, offset 0,0), so it flows through NINA's normal
        /// debayer pipeline instead of being collapsed to grayscale. Colour resolution is down-sampled
        /// to sensor-pattern resolution in the process (one channel kept per pixel), which is irrelevant
        /// for live-view framing/focus use but means this is NOT a substitute for real RAW capture data.
        /// </summary>
        private IExposureData DecodeJpegToBayerExposure(byte[] jpeg, ImageMetaData metaData) {
            using (var memStream = new MemoryStream(jpeg)) {
                memStream.Position = 0;

                JpegBitmapDecoder decoder = new JpegBitmapDecoder(memStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);

                FormatConvertedBitmap bitmap = new FormatConvertedBitmap();
                bitmap.BeginInit();
                bitmap.Source = decoder.Frames[0];
                bitmap.DestinationFormat = System.Windows.Media.PixelFormats.Rgb48; // keep full colour, 16 bit/channel
                bitmap.EndInit();

                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;
                ushort[] rgb = new ushort[width * height * 3];
                bitmap.CopyPixels(rgb, width * 3 * 2, 0);

                ushort[] bayer = new ushort[width * height];
                // RGGB: (evenRow,evenCol)=R  (evenRow,oddCol)=G  (oddRow,evenCol)=G  (oddRow,oddCol)=B
                for (int y = 0; y < height; y++) {
                    bool evenRow = (y % 2 == 0);
                    int rowBase = y * width;
                    int rgbRowBase = rowBase * 3;
                    for (int x = 0; x < width; x++) {
                        bool evenCol = (x % 2 == 0);
                        int rgbIdx = rgbRowBase + x * 3;
                        ushort value;
                        if (evenRow && evenCol) {
                            value = rgb[rgbIdx];       // R
                        } else if (!evenRow && !evenCol) {
                            value = rgb[rgbIdx + 2];   // B
                        } else {
                            value = rgb[rgbIdx + 1];   // G
                        }
                        bayer[rowBase + x] = value;
                    }
                }

                return _exposureDataFactory.CreateImageArrayExposureData(
                        input: bayer,
                        width: width,
                        height: height,
                        bitDepth: 16,
                        isBayered: true,
                        metaData: metaData);
            }
        }

        public void StopLiveView() {
            LMX_func_api_Ctrl_LiveView_Stop(out retError);
            LiveViewEnabled = false;
        }

        public Task<bool> Connect(CancellationToken token) {
            return Task.Run<bool>(() => {
                try {
                    //connect to device
                    if (NativeBinding.ExtendedMode) {
                        _connected = LumixCam.Ext_Connect(_index, out retError);
                    } else {
                        ret = LumixCam.LMX_func_api_Select_PnPDevice(_index, ref _lmxConnectDeviceInfo, out retError);
                        ret = LumixCam.LMX_func_api_Open_Session(0x00010001, out deviceConnectVer, out retError);
                        _connected = true;
                    }

                    // Extended: force JPEG capture (workaround for bodies whose .RW2 the RAW converter can't
                    // read, e.g. GH7). When PreferJpeg is OFF, force RAW back — otherwise a camera left in JPEG
                    // by a previous PreferJpeg session stays in JPEG and every frame comes back grayscale.
                    if (NativeBinding.ExtendedMode) {
                        uint q = Properties.Settings.Default.PreferJpeg ? LumixCam.IMGQ_JPEG_FINE : LumixCam.IMGQ_RAW;
                        LumixCam.Ext_SetImageQuality(q, out retError);
                        Logger.Info($"Image quality set to {(Properties.Settings.Default.PreferJpeg ? "JPEG" : "RAW")} (0x{q:X}, err={retError}).");
                    }

                    ret = LMX_func_api_SS_Get_Capability(ref SS_CapaInfo, out retError);
                    ret = LMX_func_api_ISO_Get_Capability(ref Iso_CapaInfo, out retError);
                    // Rebuild the cached lists from THIS connection's fresh capability data (a reconnect reuses
                    // the driver, and stale/empty caches left gain unpopulated).
                    _gains = null; _gainsRaw = null; _ssTable = null; _exposures = null; _warnedJpeg = false; _warnedLibRaw = false;

                    // Warn (never block) about the exposure mode. Use the dedicated Get_Mode_Pos getter, not the
                    // CameraMode capability struct (whose Tether-buffer layout differs, so CurVal_mode_pos read
                    // wrong and falsely warned "not in M"). Only the Tether DLL reports C1-C3 positions.
                    if (LMX_func_api_CameraMode_Get_Mode_Pos(out uint modePosConnect, out retError) == LMX_BOOL_TRUE) {
                        CheckExposureMode((ushort)modePosConnect);
                    }

                    // Get current parameter values
                    ret = LumixCam.LMX_func_api_ISO_Get_Param(out _curIsoValue, out retError);
                    ret = LumixCam.LMX_func_api_SS_Get_Param(out _curSsValue, out retError);
                    //not useful in nina context
                    //ret = LumixCam.LMX_func_api_Aperture_Get_Param(out _curApetureValue, out retError);
                    //ret = LumixCam.LMX_func_api_WB_Get_Param(out _curWbValue, out retError);
                    //ret = LumixCam.LMX_func_api_Exposure_Get_Param(out _curExposureCompValue, out retError);

                    // Register callback function
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD, callback);
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER, callback);
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE, callback);
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_SHUTTER, callback);
                    returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_ISO, callback);

                    // Save destination (extended mode only): SD only / PC only (cardless) / PC+SD. With a PC
                    // target the camera fires OBJCT_REQ_TRNSFER on capture and we pull the frame via Get_Object.
                    if (NativeBinding.ExtendedMode) {
                        ushort saveTgt = (ushort)Properties.Settings.Default.SaveTarget;
                        LumixCam.LMX_func_api_SetupFilesConfig_Set_Target(saveTgt, out retError);
                        Logger.Info($"Save target set to {saveTgt} (0=SD,1=PC,2=PC+SD), err={LumixError.Describe(retError)}.");
                        ReportCardCapacity(saveTgt);
                    }

                    lumixCameras = new Dictionary<string, CameraSpecs>
                         {
            { "DC-S1R", new CameraSpecs { Model = "DC-S1R", Width = 8368, Height = 5584, PixelPitch = 4.3 } },
            { "DC-S1RII", new CameraSpecs { Model = "DC-S1RII", Width = 8144, Height = 5434, PixelPitch = 4.39 } },
            { "DC-S1", new CameraSpecs { Model = "DC-S1", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S1H", new CameraSpecs { Model = "DC-S1H", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5", new CameraSpecs { Model = "DC-S5", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5M2", new CameraSpecs { Model = "DC-S5M2", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5M2X", new CameraSpecs { Model = "DC-S5M2X", Width = 6008, Height = 4008, PixelPitch = 5.94 } },
            { "DC-S9", new CameraSpecs { Model = "DC-S9", Width = 6000, Height = 4000, PixelPitch = 5.94 } },      // 24 MP full frame (same sensor as S5 II)
            { "DC-BGH1", new CameraSpecs { Model = "DC-BGH1", Width = 3680, Height = 2760, PixelPitch = 4.68 } },
            { "DC-BGS1", new CameraSpecs { Model = "DC-BGS1", Width = 6000, Height = 4000, PixelPitch = 5.91 } },
            { "DC-GH6", new CameraSpecs { Model = "DC-GH6", Width = 5776 , Height = 4336, PixelPitch = 3 } },
            { "DC-GH7", new CameraSpecs { Model = "DC-GH7", Width = 5776 , Height = 4336, PixelPitch = 3 } },
            { "DC-GH5", new CameraSpecs { Model = "DC-GH5", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
            { "DC-GH5S", new CameraSpecs { Model = "DC-GH5S", Width = 3680 , Height = 2760, PixelPitch = 4.68 } },
            { "DC-GH5M2", new CameraSpecs { Model = "GH5M2", Width = 5184, Height = 3888, PixelPitch = 3.33} },
            { "DC-G9", new CameraSpecs { Model = "DC-G9", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
            { "DC-G9M2", new CameraSpecs { Model = "DC-G9M2", Width = 5776, Height = 4336, PixelPitch = 3 } },     // 25 MP MFT (same sensor as GH7)
            { "DefaultLumix", new CameraSpecs { Model = "DefaultLumix", Width = 6000, Height = 4000, PixelPitch = 5.9 } }
        };

                    if (!lumixCameras.TryGetValue(_lmxDevInfo.dev_ModelName, out _currentCameraspecs)) {
                        Notification.ShowWarning("Camera is not known. Defaulting to full size sensor values");
                        lumixCameras.TryGetValue("DefaultLumix", out _currentCameraspecs);
                        Notification.ShowWarning("Overriding Height from Plugin Settings");
                    }
                    if (Properties.Settings.Default.SensorHeight != 0) {
                        _currentCameraspecs.Height = Properties.Settings.Default.SensorHeight;
                        Notification.ShowWarning("Overriding Height from Plugin Settings");
                    }
                    if (Properties.Settings.Default.PixelPitch != 0) {
                        _currentCameraspecs.PixelPitch = Properties.Settings.Default.PixelPitch;
                        Notification.ShowWarning("Overriding Pitch from Plugin Settings");
                    }
                    if (Properties.Settings.Default.SensorWidth != 0) {
                        _currentCameraspecs.Width = Properties.Settings.Default.SensorWidth;
                        Notification.ShowWarning("Overriding Width from Plugin Settings");
                    }

                    //not useful in Nina context
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_APERTURE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_WHITEBALANCE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_EXPOSURE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_AF_CONFIG, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_AFAE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_ZOOM, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_LENS, callback);
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
                Logger.Info($"[Lumix] Connected={_connected} ExtendedMode={NativeBinding.ExtendedMode} DLL='{NativeBinding.ActiveDllPath}' ExposureMin={ExposureMin:0.#####}s ExposureMax={ExposureMax:0}s Gain={Gain} GainMin={GainMin} GainMax={GainMax} Gains={Gains?.Count ?? 0}");
                return _connected;
            });
        }

        public void Disconnect() {
            if (!Connected) { return; }
            Logger.Info("[Lumix] Disconnect: start");
            _connected = false;                 // report disconnected immediately — NINA's Disconnect() never blocks
            try { bulbCompletionCTS?.Cancel(); } catch { }
            // Native teardown is fire-and-forget: some SDK calls (CloseSession after a bulb) can block for a long
            // time, and we must never freeze NINA. If a call stalls it leaks a worker thread, but the UI stays
            // responsive. Each step is logged so the last line before it stops shows which call stalls.
            Task.Run(() => {
                try {
                    Logger.Info("[Lumix] Disconnect: delete callbacks");
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD);
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER);
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE);
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_SHUTTER);
                    LumixCam.LMX_func_api_Delete_CallBackInfo((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_ISO);
                    if (NativeBinding.ExtendedMode) {
                        // Take the camera out of BULB before closing the session — a lingering bulb state appears
                        // to hang CloseSession (a plain, non-bulb disconnect closes fine).
                        try {
                            byte sr = LMX_func_api_SS_Set_Param((1 * 1000) | 0x80000000, out uint sserr);
                            Logger.Info($"[Lumix] Disconnect: exit BULB (SS=1s) ret={sr} err={sserr}");
                        } catch (Exception sx) { Logger.Error("[Lumix] Disconnect: exit-BULB threw: " + sx); }
                        if (Properties.Settings.Default.SaveTarget != LumixCam.SAVE_TARGET_SD) {
                            Logger.Info("[Lumix] Disconnect: restore SD target");
                            LumixCam.LMX_func_api_SetupFilesConfig_Set_Target(LumixCam.SAVE_TARGET_SD, out retError);
                        }
                        // Let any in-flight bulb close/finalize fully settle before tearing down the session,
                        // then step through the close so the log shows which call (Session vs Device) stalls.
                        try { bulbCompletionTask?.Wait(3000); } catch { }
                        System.Threading.Thread.Sleep(400);
                        Logger.Info("[Lumix] Disconnect: CloseSession");
                        LumixCam.Ext_CloseSessionOnly(out retError);
                        Logger.Info("[Lumix] Disconnect: CloseSession done; CloseDevice");
                        LumixCam.Ext_CloseDeviceOnly(out retError);
                        Logger.Info("[Lumix] Disconnect: CloseDevice done");
                    } else {
                        Logger.Info("[Lumix] Disconnect: Close_Session/Device");
                        LumixCam.LMX_func_api_Close_Session(out retError);
                        LumixCam.LMX_func_api_Close_Device(out retError);
                    }
                    Logger.Info("[Lumix] Disconnect: native teardown done");
                } catch (Exception ex) { Logger.Error("[Lumix] Disconnect teardown threw: " + ex); }
            });
            Logger.Info("[Lumix] Disconnect: returned (teardown running async)");
        }
    }
}