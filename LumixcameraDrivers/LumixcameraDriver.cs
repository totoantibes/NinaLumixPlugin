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
        private byte[] buffer;
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
                    //Notification.ShowInformation("LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER");
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
            uint formatType;
            uint dataSize;
            LMX_STRUCT_PTP_ARRAY_STRING fileNameArrStr = new LMX_STRUCT_PTP_ARRAY_STRING();

            Logger.Debug("image added");
            LMX_func_api_Get_Object_FormatType(cb_event_param, out formatType, out retError);
            LMX_func_api_Get_Object_DataSize(cb_event_param, out dataSize, out retError);
            LMX_func_api_Get_Object_FileName(cb_event_param, ref fileNameArrStr, out retError);
            buffer = new byte[dataSize];
            if (!formatType.IsEqual(Lmx_def_lib_object_format.LMX_DEF_OBJ_FORMAT_RAW)) {
                Notification.ShowWarning("The Image format is not set to RAW");
            }

            try { LMX_func_api_Get_Object(cb_event_param, ref buffer[0], dataSize, out retError); } catch (Exception ex) { }

            _downloadExposure.TrySetResult(null);
            return;
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

        public double ExposureMin {
            get {
                double _exp = 120;
                if (Connected) {
                    if (_exposures.IsEqual(null)) {
                        _exposures = new List<int>();
                        foreach (uint val in SS_CapaInfo.Capa_Enum.SupportVal) {
                            //if (val != 0 && !val.IsEqual(Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_UNKNOWN) && !val.IsEqual(Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_BULB) && !val.IsEqual(Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_AUTO)) {
                            if ((val & 0x80000000) != 0x00000000) {
                                _exposures.Add((int)(val & 0x7fffffff) / 1000);
                                //_exp = Math.Max(val, _exp);
                            }
                        }
                    }
                    _exp = SS_CapaInfo.CurVal_Range.MaxVal;
                    return ((double)(1 / (_exp / 1000)));
                } else { return 0; }
            }
        }

        public double ExposureMax {
            get {
                if (Connected) {
                    // TO DO in case BULB is supported

                    //if (SS_CapaInfo.Capa_Enum.SupportVal[0] == (int)((Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_BULB))) {
                    //    bulbFound = true;
                    //    return double.PositiveInfinity;
                    //} else {
                    return ((double)(SS_CapaInfo.CurVal_Range.MinVal & 0x7fffffff) / 1000);
                    //}
                } else { return 0; }
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

        public string Name {
            get {
                if (Connected) {
                    return _lmxDevInfo.dev_ModelName;
                } else {
                    return string.Empty;
                }
            }
        }

        public string DisplayName {
            get {
                if (Connected) {
                    return _lmxDevInfo.dev_ModelName;
                } else {
                    return string.Empty;
                }
            }
        }

        public string Category { get => "Lumix"; }

        public bool Connected {
            get {
                return !_lmxConnectDeviceInfo.IsEqual(null);
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

                Logger.Debug("Prepare start of exposure: " + sequence);
                _downloadExposure = new TaskCompletionSource<object>();
                if (exposureTime <= 60.0 && exposureTime >= 1) {
                    if (_exposures.Contains((int)exposureTime)) {
                        Logger.Debug(" 1 <= Exposuretime <= 60. Setting automatic shutter speed.");
                        LMX_func_api_SS_Set_Param((((int)(exposureTime) * 1000) | 0x80000000), out uint retError);
                    } else {
                        Notification.ShowWarning("Exposure is not in the allowed list of exposures. Will take a 1 sec exposure or the last good setting");
                    }
                } else {
                    if (exposureTime < 1) {
                        Logger.Debug(" Exposuretime < 1. Setting automatic shutter speed.");
                        LMX_func_api_SS_Set_Param((int)(1000 / exposureTime), out uint retError);
                    } else {
                        Notification.ShowWarning("Bulb Mode is not supported by the SDK. Max exposure time in 60s");
                        ///*Stop Exposure after exposure time or upon cancellation*/
                        //try { bulbCompletionCTS?.Cancel(); } catch { }
                        //bulbCompletionCTS = new CancellationTokenSource();
                        //Logger.Debug("Use Bulb capture");
                        //LMX_func_api_SS_Set_Param((int)(Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_BULB), out uint retError);

                        //try {
                        //    Logger.Debug("Starting bulb capture");
                        //    Task.Run(() => LMX_func_api_Rec_Ctrl_Release(ref lmx_rec_ctrl, out retError));
                        //} catch (Exception ex) {
                        //    Logger.Error(ex);
                        //}
                        ///*Stop Exposure after exposure time */
                        //bulbCompletionTask = Task.Run(async () => {
                        //    await CoreUtil.Wait(TimeSpan.FromSeconds(exposureTime), bulbCompletionCTS.Token);
                        //    if (!bulbCompletionCTS.IsCancellationRequested) {
                        //        StopExposure();
                        //    }
                        //}, bulbCompletionCTS.Token);

                        return;
                    }
                }
                // for non bulb
                Logger.Debug("Start capture");
                ret = LMX_func_api_Rec_Ctrl_Release(ref lmx_rec_ctrl, out retError);
                if (retError == LMX_BOOL_TRUE) {
                    Notification.ShowWarning("Can't execute Single-shot command");
                }
            }
        }

        public async Task WaitUntilExposureIsReady(CancellationToken token) {
            using (token.Register(() => AbortExposure())) {
                await _downloadExposure.Task;
            }
        }

        public void StopExposure() {
            LMX_STRUCT_SS_CAPA_INFO pSS_CapaInfo = new LMX_STRUCT_SS_CAPA_INFO();
            LMX_func_api_SS_Get_Capability(ref pSS_CapaInfo, out retError);

            if (Connected && pSS_CapaInfo.Capa_Enum.IsEqual(Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_BULB)) {
                LMX_STRUCT_REC_CTRL lmx_rec_ctrl = new LMX_STRUCT_REC_CTRL();
                lmx_rec_ctrl.CtrlID = ((uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_ONESHOT);
                lmx_rec_ctrl.ParamData.NumOfVal = 0;
                ret = LMX_func_api_Rec_Ctrl_Release(ref lmx_rec_ctrl, out retError);
                if (retError == LMX_BOOL_FALSE) {
                    Notification.ShowWarning("Can't stop exposure");
                }
            }
        }

        public void AbortExposure() {
            StopExposure();
        }

        public async Task<IExposureData> DownloadExposure(CancellationToken token) {
            if (_downloadExposure.Task.IsCanceled) { return null; }
            Logger.Debug("Waiting for download of exposure");
            await _downloadExposure.Task;
            Logger.Debug("Downloading of exposure complete. Converting image to internal array");

            try {
                var rawImageData = buffer;
                var metaData = new ImageMetaData();
                metaData.FromCamera(this);
                return _exposureDataFactory.CreateRAWExposureData(
                    converter: _profileService.ActiveProfile.CameraSettings.RawConverter,
                    rawBytes: rawImageData,
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
                    ret = LumixCam.LMX_func_api_Select_PnPDevice(_index, ref _lmxConnectDeviceInfo, out retError);
                    ret = LumixCam.LMX_func_api_Open_Session(0x00010001, out deviceConnectVer, out retError);

                    ret = LMX_func_api_SS_Get_Capability(ref SS_CapaInfo, out retError);
                    ret = LMX_func_api_ISO_Get_Capability(ref Iso_CapaInfo, out retError);
                    ret = LMX_func_api_CameraMode_Get_Capability(ref CM_CapaInfo, out retError);

                    if (!CM_CapaInfo.CurVal_mode_drive.IsEqual(Lmx_def_lib_Camera_Mode_Info_Drive_Mode.LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_SINGLE)) {
                        Notification.ShowWarning("Camera is not in Single Shot Mode. Best to change");
                    }
                    if (CM_CapaInfo.CurVal_mode_pos.IsEqual(Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_A) || CM_CapaInfo.CurVal_mode_pos.IsEqual(Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_P) || CM_CapaInfo.CurVal_mode_pos.IsEqual(Lmx_def_lib_Camera_Mode_Info_Mode_Pos.LMX_DEF_CAMERA_MODE_INFO_MODE_POS_S)) {
                        Notification.ShowWarning("Camera is not in M mode. Best To Change");
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

                    lumixCameras = new Dictionary<string, CameraSpecs>
                         {
            { "DC-S1R", new CameraSpecs { Model = "DC-S1R", Width = 8368, Height = 5584, PixelPitch = 4.3 } },
            { "DC-S1", new CameraSpecs { Model = "DC-S1", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S1H", new CameraSpecs { Model = "DC-S1H", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5", new CameraSpecs { Model = "DC-S5", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5 II", new CameraSpecs { Model = "DC-S5 II", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S5 IIX", new CameraSpecs { Model = "DC-S5 IIX", Width = 6000, Height = 4000, PixelPitch = 5.94 } },
            { "DC-S9", new CameraSpecs { Model = "DC-S9", Width = 8192, Height = 5464, PixelPitch = (17.3/8192)*1000} },
            { "DC-BGH1", new CameraSpecs { Model = "DC-BGH1", Width = 3680, Height = 2760, PixelPitch = 4.68 } },
            { "DC-BGS1", new CameraSpecs { Model = "DC-BGS1", Width = 6000, Height = 4000, PixelPitch = 5.91 } },
            { "DC-GH6", new CameraSpecs { Model = "DC-GH6", Width = 5776 , Height = 4336, PixelPitch = 3 } },
            { "DC-GH5", new CameraSpecs { Model = "DC-GH5", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
            { "DC-GH5S", new CameraSpecs { Model = "DC-GH5S", Width = 3680 , Height = 2760, PixelPitch = 4.68 } },
            { "DC-GH5 II", new CameraSpecs { Model = "GH5 II", Width = 5184, Height = 3888, PixelPitch = 3.33} },
            { "DC-G9", new CameraSpecs { Model = "DC-G9", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
            { "DC-G9 II", new CameraSpecs { Model = "DC-G9 II", Width = 5184, Height = 3888, PixelPitch = 3.33 } },
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
                return !_lmxConnectDeviceInfo.IsEqual(null);
            });
        }

        public void Disconnect() {
            if (Connected) {
                try {
                    //returnV = LumixCam.LMX_func_api_Delete_CallBackInfo(callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_SHUTTER, callback);
                    //returnV = LumixCam.LMX_func_api_Reg_NotifyCallback((uint)Lmx_event_id.LMX_DEF_LIB_EVENT_ID_ISO, callback);

                    ret = LumixCam.LMX_func_api_Close_Session(out retError);
                } catch (Exception ex) {
                    Logger.Error(ex);
                    _lmxConnectDeviceInfo.Equals(null);
                    _lmxDevInfo.Equals(null);
                }
            }
        }
    }
}