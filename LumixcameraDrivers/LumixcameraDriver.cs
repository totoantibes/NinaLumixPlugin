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

        public short MaxBinX => throw new NotImplementedException();

        public short MaxBinY => throw new NotImplementedException();

        public double PixelSizeX => throw new NotImplementedException();

        public double PixelSizeY => throw new NotImplementedException();

        public bool CanSetTemperature => throw new NotImplementedException();

        public bool CoolerOn { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public double CoolerPower => throw new NotImplementedException();

        public bool HasDewHeater => throw new NotImplementedException();

        public bool DewHeaterOn { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public CameraStates CameraState => throw new NotImplementedException();

        public bool CanSubSample => throw new NotImplementedException();

        public bool EnableSubSample { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SubSampleX { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SubSampleY { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SubSampleWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int SubSampleHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool CanShowLiveView => throw new NotImplementedException();

        public bool LiveViewEnabled => throw new NotImplementedException();

        public bool HasBattery => throw new NotImplementedException();

        public int BatteryLevel => throw new NotImplementedException();

        public int BitDepth => throw new NotImplementedException();

        public bool CanSetOffset => throw new NotImplementedException();

        public int Offset { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int OffsetMin => throw new NotImplementedException();

        public int OffsetMax => throw new NotImplementedException();

        public bool CanSetUSBLimit => throw new NotImplementedException();

        public int USBLimit { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int USBLimitMin => throw new NotImplementedException();

        public int USBLimitMax => throw new NotImplementedException();

        public int USBLimitStep => throw new NotImplementedException();

        public bool CanGetGain => throw new NotImplementedException();

        public bool CanSetGain => throw new NotImplementedException();

        public int GainMax => throw new NotImplementedException();

        public int GainMin => throw new NotImplementedException();

        public int Gain { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public double ElectronsPerADU => throw new NotImplementedException();

        public IList<string> ReadoutModes => throw new NotImplementedException();

        public short ReadoutMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public short ReadoutModeForSnapImages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public short ReadoutModeForNormalImages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IList<int> Gains => throw new NotImplementedException();

        public AsyncObservableCollection<BinningMode> BinningModes => throw new NotImplementedException();

        public bool HasSetupDialog => throw new NotImplementedException();

        public string Id => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public string DisplayName => throw new NotImplementedException();

        public string Category => throw new NotImplementedException();

        public bool Connected => throw new NotImplementedException();

        public string Description => throw new NotImplementedException();

        public string DriverInfo => throw new NotImplementedException();

        public string DriverVersion => throw new NotImplementedException();

        public IList<string> SupportedActions => throw new NotImplementedException();

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
                    ret = LumixCam.LMX_func_api_Select_PnPDevice(_index, ref _lmxConnectDeviceInfo, out retError);
                    ret = LumixCam.LMX_func_api_Open_Session(0x00010001, out deviceConnectVer, out retError);
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
                return _lmxConnectDeviceInfo.IsEqual(null);
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