using NINA.Core.Enum;
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

namespace Roberthasson.NINA.Lumixcamera.LumixcameraDrivers {

    /// <summary>
    /// This Class shows the basic principle on how to add a new Device driver to N.I.N.A. via the plugin interface
    /// The DeviceProvider will return an instance of this class as a sample weather device
    /// For this example the weather data will generate random numbers
    /// </summary>
    public class LumixcameraDriver : BaseINPC, ICamera {
        private IProfileService _profileService;
        private readonly IExposureDataFactory _exposureDataFactory;
        private LumixCam.LMX_DEVINFO _lmxDevInfo = new LMX_DEVINFO();
        private LumixCam.LMX_CONNECT_DEVICE_INFO _lmxConnectDeviceInfo = new LMX_CONNECT_DEVICE_INFO();
        private uint retError, deviceConnectVer;
        private byte ret;
        private uint _index;
        private uint _curIsoValue = 0;
        private uint _curSsValue = 0;
        private AsyncObservableCollection<BinningMode> _binningModes;
        //private uint _curApetureValue = 0 useless in nina context;
        //private uint _curWbValue = 0; useless in Nina context
        //private uint _curExposureCompValue = 0;useless in Nina context

        private LMX_STRUCT_ISO_CAPA_INFO Iso_CapaInfo = new LMX_STRUCT_ISO_CAPA_INFO();
        private LMX_STRUCT_SS_CAPA_INFO SS_CapaInfo = new LMX_STRUCT_SS_CAPA_INFO();

        private static string GetStringSS(ulong value) {
            string str = string.Empty;

            switch (value) {
                case ((ulong)Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_BULB):
                    str = "Bulb";
                    break;

                case ((ulong)Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_AUTO):
                    str = "Auto";
                    break;

                case ((ulong)Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_UNKNOWN):
                    str = "Unknown";
                    break;

                default:
                    if ((value & 0x80000000) == 0x00000000) {
                        str = "1/" + (value / 1000).ToString();
                    } else {
                        value = (value & 0x7fffffff);
                        str = (value / 1000).ToString();
                    }
                    str += " sec";
                    break;
            }

            return str;
        }

        public LumixcameraDriver(IProfileService profileService, IExposureDataFactory exposureDataFactory, LumixCam.LMX_DEVINFO lmxDevInfo, LumixCam.LMX_CONNECT_DEVICE_INFO lmxConnetDeviceInfo, int index) {
            _profileService = profileService;
            _exposureDataFactory = exposureDataFactory;
            _lmxDevInfo = lmxDevInfo;
            _lmxConnectDeviceInfo = lmxConnetDeviceInfo;
            _index = ((uint)index);
            //  ret = LumixCam.LMX_func_api_Select_PnPDevice(((uint)i), ref lmxConnetDeviceInfo, out retError);
            //  ret = LumixCam.LMX_func_api_Open_Session(0x00010001, out deviceConnectVer,out retError);

            // Register callback function
            // ret = LumixCam.LMX_func_api_Reg_NotifyCallback(LumixCam.Lmx_event_id::LMX_DEF_LIB_EVENT_ID_OBJCT_ADD, NotifyCallbackFunction);
            //ret = LumixCam.LMX_func_api_Reg_NotifyCallback(LumixCam.Lmx_event_id::LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER, NotifyCallbackFunction);
        }

        public static int NotifyCallbackFunction(uint cb_event_type, uint cb_event_param) {
            switch ((Lmx_event_id)cb_event_type) {
                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER:
                case Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD:
                    //      CopyFileToPC(cb_event_type, cb_event_param);
                    break;

                default:
                    return -1;
            }

            return 0;
        }

        public bool HasShutter => true;

        public double Temperature {
            get => double.NaN;
        }

        public double TemperatureSetPoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public short BinX { get => 1; set => throw new NotImplementedException(); }
        public short BinY { get => 1; set => throw new NotImplementedException(); }

        public string SensorName {
            get {
                if (_lmxDevInfo.dev_ModelName != string.Empty) {
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
                if (!_lmxConnectDeviceInfo.IsEqual(null)) {
                    return 3680;//_camera.ImageSize.Width;
                } else {
                    return 0;
                }
            }
        }

        public int CameraYSize {
            get {
                if (!_lmxConnectDeviceInfo.IsEqual(null)) {
                    return 2760;//_camera.ImageSize.Width;
                } else {
                    return 0;
                }
            }
        }

        public double ExposureMin => throw new NotImplementedException();

        public double ExposureMax => throw new NotImplementedException();

        public short MaxBinX { get => 1; set => throw new NotImplementedException(); }

        public short MaxBinY { get => 1; set => throw new NotImplementedException(); }

        public double PixelSizeX => 4.68;//hard coded for now

        public double PixelSizeY => 4.68;//hard coded for now

        public bool CanSetTemperature => false;

        public bool CoolerOn { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public double CoolerPower => throw new NotImplementedException();

        public bool HasDewHeater => throw new NotImplementedException();

        public bool DewHeaterOn { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public CameraStates CameraState => CameraStates.NoState; // TODO

        public bool CanSubSample => throw new NotImplementedException();

        public bool EnableSubSample { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SubSampleX { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SubSampleY { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SubSampleWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SubSampleHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool CanShowLiveView => false; //TO DO

        public bool LiveViewEnabled => throw new NotImplementedException();

        public bool HasBattery => true;

        public int BatteryLevel => 100; //hardcoded for now

        public int BitDepth => 14; // hardcoded for now
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
                if (_lmxDevInfo.dev_ModelName != string.Empty) {
                    return true;
                } else { return false; }
            }
        }

        //=> throw new NotImplementedException();

        public bool CanSetGain => CanGetGain;

        public int GainMax {
            get {
                if (_lmxDevInfo.dev_ModelName != string.Empty) {
                    return ((int)Iso_CapaInfo.Capa_Enum.SupportVal.Max());
                } else { return 0; }
            }
        }

        public int GainMin {
            get {
                if (_lmxDevInfo.dev_ModelName != string.Empty) {
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

        public short ReadoutModeForSnapImages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public short ReadoutModeForNormalImages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IList<int> Gains {
            get {
                List<int> gains = new List<int>();
                foreach (int val in Iso_CapaInfo.Capa_Enum.SupportVal) { Gains.Add(val); }
                return Gains;
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
                if (_lmxDevInfo.dev_ModelName != string.Empty) {
                    return _lmxDevInfo.dev_ModelName;
                } else {
                    return string.Empty;
                }
            }
        }

        public string DisplayName {
            get {
                if (_lmxDevInfo.dev_ModelName != string.Empty) {
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

        public string Description => "Description";

        public string DriverInfo => "Driver Info";

        public string DriverVersion => "DriverVersion";

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
            throw new NotImplementedException();
        }

        public void StartExposure(CaptureSequence sequence) {
            if (!_lmxConnectDeviceInfo.IsEqual(null)) {
                LMX_STRUCT_REC_CTRL lmx_rec_ctrl = new LMX_STRUCT_REC_CTRL();
                lmx_rec_ctrl.CtrlID = ((uint)Lmx_TagID_Rec_Ctrl_Release.LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_ONESHOT);
                lmx_rec_ctrl.ParamData.NumOfVal = 0;
                ret = LMX_func_api_Rec_Ctrl_Release(ref lmx_rec_ctrl, out retError);
                if (retError == LMX_BOOL_FALSE) {
                    Notification.ShowWarning("Can't execute Single-shot command");
                }
            }
        }

        public Task WaitUntilExposureIsReady(CancellationToken token) {
            throw new NotImplementedException();
        }

        public void StopExposure() {
            LMX_STRUCT_SS_CAPA_INFO pSS_CapaInfo = new LMX_STRUCT_SS_CAPA_INFO();
            LMX_func_api_SS_Get_Capability(ref pSS_CapaInfo, out retError);

            if (!_lmxConnectDeviceInfo.IsEqual(null) && !pSS_CapaInfo.Capa_Enum.IsEqual(Lmx_def_lib_DevpropEx_ShutterSpeed_param.LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_BULB)) {
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

        public Task<IExposureData> DownloadExposure(CancellationToken token) {
            throw new NotImplementedException();
        }

        public void StartLiveView(CaptureSequence sequence) {
            throw new NotImplementedException();
        }

        public Task<IExposureData> DownloadLiveView(CancellationToken token) {
            throw new NotImplementedException();
        }

        public void StopLiveView() {
            throw new NotImplementedException();
        }

        public Task<bool> Connect(CancellationToken token) {
            //throw new NotImplementedException();
            return Task.Run<bool>(() => {
                try {
                    //connect to device
                    ret = LumixCam.LMX_func_api_Select_PnPDevice(_index, ref _lmxConnectDeviceInfo, out retError);
                    ret = LumixCam.LMX_func_api_Open_Session(0x00010001, out deviceConnectVer, out retError);

                    ret = LMX_func_api_SS_Get_Capability(ref SS_CapaInfo, out retError);
                    ret = LMX_func_api_ISO_Get_Capability(ref Iso_CapaInfo, out retError);

                    // Register callback function
                    // Get current parameter values
                    ret = LumixCam.LMX_func_api_ISO_Get_Param(out _curIsoValue, out retError);
                    ret = LumixCam.LMX_func_api_SS_Get_Param(out _curSsValue, out retError);

                    //useless in nina context
                    //ret = LumixCam.LMX_func_api_Aperture_Get_Param(out _curApetureValue, out retError);
                    //ret = LumixCam.LMX_func_api_WB_Get_Param(out _curWbValue, out retError);
                    //ret = LumixCam.LMX_func_api_Exposure_Get_Param(out _curExposureCompValue, out retError);

                    //ret = LumixCam.LMX_func_api_Reg_NotifyCallback(LumixCam.Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_ADD, NotifyCallbackFunction);
                    //ret = LumixCam.LMX_func_api_Reg_NotifyCallback(LumixCam.Lmx_event_id.LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER, NotifyCallbackFunction);
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
                return !_lmxConnectDeviceInfo.IsEqual(null);
            });
        }

        public void Disconnect() {
            //throw new NotImplementedException();
            if (!_lmxConnectDeviceInfo.IsEqual(null)) {
                try {
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