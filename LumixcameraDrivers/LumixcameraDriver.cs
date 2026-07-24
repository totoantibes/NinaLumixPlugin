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
        private bool bulbFound = false;
        private bool _connected = false;
        private byte[] buffer;
        private uint _lastFormat;   // object format of the most recent capture (RAW vs JPEG) for download decode
        private AsyncObservableCollection<BinningMode> _binningModes;

        private TaskCompletionSource<bool> _cameraConnected;
        private MemoryStream _memoryStream;
        private LMX_CALLBACK_FUNC callback;

        private LMX_STRUCT_ISO_CAPA_INFO Iso_CapaInfo = new LMX_STRUCT_ISO_CAPA_INFO();
        private LMX_STRUCT_SS_CAPA_INFO SS_CapaInfo = new LMX_STRUCT_SS_CAPA_INFO();
        private LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO CM_CapaInfo = new LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO();
        private int _prevShutterSpeed;
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

        public int NotifyCallbackFunction(uint cb_event_type, uint cb_event_param) {
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
                        LMX_func_api_Skip_Object_Transfer(cb_event_param, out uint _skipErr);
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
                Logger.Warning("[LumixCapture] object data size is 0 — nothing to download.");
                _downloadExposure.TrySetResult(null);
                return;
            }
            buffer = new byte[dataSize];
            byte gr = 0;
            try { gr = LMX_func_api_Get_Object(cb_event_param, ref buffer[0], dataSize, out retError); } catch (Exception ex) { Logger.Error("[LumixCapture] Get_Object threw: " + ex); }
            if (dataSize >= 4) {
                Logger.Info($"[LumixCapture] Get_Object ret={gr} err={retError}; magic={buffer[0]:X2} {buffer[1]:X2} {buffer[2]:X2} {buffer[3]:X2}");
            }

            _downloadExposure.TrySetResult(null);
            return;
        }

        // ---- Extended-mode helpers: manual-mode whitelist + shutter-speed snapping ----

        private ushort _lastNotifiedModePos = 0xFFFF;
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

        public short BayerOffsetX { get => 1; set => throw new NotImplementedException(); }

        public short BayerOffsetY { get => 1; set => throw new NotImplementedException(); }

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

        public int BatteryLevel => 100; //hardcoded for now

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

        public int GainMax {
            get {
                if (Connected) {
                    return ((int)Iso_CapaInfo.Capa_Enum.SupportVal.Max());
                } else { return 0; }
            }
        }

        public int GainMin {
            get {
                if (Connected) {
                    return ((int)Iso_CapaInfo.Capa_Enum.SupportVal.Min());
                } else { return 0; }
            }
        }

        public int Gain {
            get {
                return ((int)Iso_CapaInfo.CurVal);
            }
            set {
                ret = LMX_func_api_ISO_Set_Param(((uint)value), out retError);
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

        public IList<int> Gains {
            get {
                if (_gains == null) {
                    _gains = new List<int>();

                    foreach (uint val in Iso_CapaInfo.Capa_Enum.SupportVal) {
                        switch (val) {
                            case ((uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_AUTO):
                                //str = "0";// "Auto";
                                //Gains.Add(0);
                                break;

                            case (uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_I_ISO:
                                //str = "0";//"i-ISO";
                                //Gains.Add(0);
                                break;

                            case (uint)Lmx_def_lib_ISO_param.LMX_DEF_ISO_UNKNOWN:
                                //str = "0";// "Unknown";
                                break;

                            default:
                                _gains.Add(((int)(val & 0x0FFFFFFF)));
                                break;
                        }
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
                    Logger.Warning($"Single-shot release did not succeed (ret={ret}, err={retError}).");
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
                    Logger.Debug("Captured object is JPEG — decoding to image array for display.");
                    return DecodeJpegToExposure(buffer, metaData);
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

        /// <summary>Decode a JPEG buffer to a 16-bit greyscale image array exposure (same path as live view).
        /// Used as the display fallback when NINA's RAW converter can't handle a camera's .RW2.</summary>
        private IExposureData DecodeJpegToExposure(byte[] jpeg, ImageMetaData metaData) {
            using (var memStream = new MemoryStream(jpeg)) {
                memStream.Position = 0;
                var decoder = new JpegBitmapDecoder(memStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                var bitmap = new FormatConvertedBitmap();
                bitmap.BeginInit();
                bitmap.Source = decoder.Frames[0];
                bitmap.DestinationFormat = System.Windows.Media.PixelFormats.Gray16;
                bitmap.EndInit();
                ushort[] outArray = new ushort[bitmap.PixelWidth * bitmap.PixelHeight];
                bitmap.CopyPixels(outArray, 2 * bitmap.PixelWidth, 0);
                return _exposureDataFactory.CreateImageArrayExposureData(
                    input: outArray,
                    width: bitmap.PixelWidth,
                    height: bitmap.PixelHeight,
                    bitDepth: 16,
                    isBayered: false,
                    metaData: metaData);
            }
        }

        public void StartLiveView(CaptureSequence sequence) {
            LMX_func_api_Ctrl_LiveView_Start(out retError);
            LiveViewEnabled = true;
        }

        public Task<IExposureData> DownloadLiveView(CancellationToken token) {
            return Task.Run<IExposureData>(() => {
                // liveview structures
                uint returnedJpegSize;
                byte[] jpegDataPos = new byte[LMX_DEF_LIVEVIEW_STREAMDATA_SIZE_MAX];
                LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM histgramBuf = new LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM();
                uint histgramBufSize = LMX_DEF_LIVEVIEW_HISTGRAM_ELEMENT_SIZE;
                LMX_STRUCT_LIVEVIEW_INFO_POSTURE postureBuf = new LMX_STRUCT_LIVEVIEW_INFO_POSTURE();
                uint postureBufSize = 32;
                LMX_STRUCT_LIVEVIEW_INFO_LEVEL levelBuf = new LMX_STRUCT_LIVEVIEW_INFO_LEVEL();
                uint levelBufSize = 32;// sizeof(LMX_STRUCT_LIVEVIEW_INFO_LEVEL);
                LMX_func_api_Get_LiveView_data(ref histgramBuf, out histgramBufSize, ref postureBuf, out postureBufSize, ref levelBuf, out levelBufSize, ref jpegDataPos[0], out returnedJpegSize, out retError);
                using (var memStream = new MemoryStream(jpegDataPos)) {
                    memStream.Position = 0;

                    JpegBitmapDecoder decoder = new JpegBitmapDecoder(memStream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);

                    FormatConvertedBitmap bitmap = new FormatConvertedBitmap();
                    bitmap.BeginInit();
                    bitmap.Source = decoder.Frames[0];
                    bitmap.DestinationFormat = System.Windows.Media.PixelFormats.Gray16;
                    bitmap.EndInit();

                    ushort[] outArray = new ushort[bitmap.PixelWidth * bitmap.PixelHeight];
                    bitmap.CopyPixels(outArray, 2 * bitmap.PixelWidth, 0);

                    var metaData = new ImageMetaData();
                    metaData.FromCamera(this);
                    return _exposureDataFactory.CreateImageArrayExposureData(
                            input: outArray,
                            width: bitmap.PixelWidth,
                            height: bitmap.PixelHeight,
                            bitDepth: 16,
                            isBayered: false,
                            metaData: metaData);
                }
            });
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

                    // Extended: optionally force JPEG capture so NINA always gets a decodable image
                    // (workaround for bodies whose .RW2 the RAW converter can't read, e.g. GH7 — issue #1).
                    if (NativeBinding.ExtendedMode && Properties.Settings.Default.PreferJpeg) {
                        LumixCam.Ext_SetImageQuality(LumixCam.IMGQ_JPEG_FINE, out retError);
                        Logger.Info($"PreferJpeg: set still quality to JPEG (err={retError}).");
                    }

                    ret = LMX_func_api_SS_Get_Capability(ref SS_CapaInfo, out retError);
                    ret = LMX_func_api_ISO_Get_Capability(ref Iso_CapaInfo, out retError);

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
                        Logger.Info($"Save target set to {saveTgt} (0=SD,1=PC,2=PC+SD), err={retError}.");
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
            { "DC-S9", new CameraSpecs { Model = "DC-S9", Width = 8192, Height = 5464, PixelPitch = (17.3/8192)*1000} },
            { "DC-BGH1", new CameraSpecs { Model = "DC-BGH1", Width = 3680, Height = 2760, PixelPitch = 4.68 } },
            { "DC-BGS1", new CameraSpecs { Model = "DC-BGS1", Width = 6000, Height = 4000, PixelPitch = 5.91 } },
            { "DC-GH6", new CameraSpecs { Model = "DC-GH6", Width = 5776 , Height = 4336, PixelPitch = 3 } },
            { "DC-GH7", new CameraSpecs { Model = "DC-GH7", Width = 5776 , Height = 4336, PixelPitch = 3 } },
            { "DC-GH5", new CameraSpecs { Model = "DC-GH5", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
            { "DC-GH5S", new CameraSpecs { Model = "DC-GH5S", Width = 3680 , Height = 2760, PixelPitch = 4.68 } },
            { "DC-GH5M2", new CameraSpecs { Model = "GH5M2", Width = 5184, Height = 3888, PixelPitch = 3.33} },
            { "DC-G9", new CameraSpecs { Model = "DC-G9", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
            { "DC-G9M2", new CameraSpecs { Model = "DC-G9M2", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
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
                Logger.Info($"[Lumix] Connected={_connected} ExtendedMode={NativeBinding.ExtendedMode} DLL='{NativeBinding.ActiveDllPath}' ExposureMin={ExposureMin:0.#####}s ExposureMax={ExposureMax:0}s");
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