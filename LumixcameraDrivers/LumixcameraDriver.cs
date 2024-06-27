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
using LumixDriverWrapper;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraDrivers {

    /// <summary>
    /// This Class shows the basic principle on how to add a new Device driver to N.I.N.A. via the plugin interface
    /// The DeviceProvider will return an instance of this class as a sample weather device
    /// For this example the weather data will generate random numbers
    /// </summary>
    public class LumixcameraDriver : BaseINPC, ICamera {
        private IProfileService _profileService;
        private readonly IExposureDataFactory _exposureDataFactory;

        public LumixcameraDriver(IProfileService profileService, IExposureDataFactory exposureDataFactory) {
            _profileService = profileService;
            _exposureDataFactory = exposureDataFactory;
            // _device = device;
        }

        public bool HasShutter => throw new NotImplementedException();

        public double Temperature => throw new NotImplementedException();

        public double TemperatureSetPoint { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public short BinX { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public short BinY { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string SensorName => throw new NotImplementedException();

        public SensorType SensorType => throw new NotImplementedException();

        public short BayerOffsetX => throw new NotImplementedException();

        public short BayerOffsetY => throw new NotImplementedException();

        public int CameraXSize => throw new NotImplementedException();

        public int CameraYSize => throw new NotImplementedException();

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
            throw new NotImplementedException();
        }

        public Task WaitUntilExposureIsReady(CancellationToken token) {
            throw new NotImplementedException();
        }

        public void StopExposure() {
            throw new NotImplementedException();
        }

        public void AbortExposure() {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void Disconnect() {
            throw new NotImplementedException();
        }
    }
}