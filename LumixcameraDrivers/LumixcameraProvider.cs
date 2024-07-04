using NINA.Core.Utility;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LumixWrapper;
using System.Security.Cryptography.Xml;

namespace Roberthasson.NINA.Lumixcamera.LumixcameraDrivers {

    /// <summary>
    /// This Class shows the basic principle on how to add a new Device driver to N.I.N.A. via the plugin interface
    /// When the application scans for equipment the "GetEquipment" method of a device provider is called.
    /// This method should then return the specific List of Devices that you can connect to
    /// </summary>
    [Export(typeof(IEquipmentProvider))]
    public class LumixcameraProvider : IEquipmentProvider<ICamera> {
        private IProfileService profileService;
        private IExposureDataFactory exposureDataFactory;
        private LumixCam driver = new LumixCam();

        [ImportingConstructor]
        public LumixcameraProvider(IProfileService profileService, IExposureDataFactory exposureDataFactory) {
            this.profileService = profileService;
            this.exposureDataFactory = exposureDataFactory;

            try {
                LumixCam.LMX_func_api_Init();
            } catch (Exception ex) {
            }
        }

        public string Name => "LumixCameraPlugin";

        public IList<ICamera> GetEquipment() {
            var devices = new List<ICamera>();
            uint retError;
            LumixCam.LMX_CONNECT_DEVICE_INFO devInfo = new LumixCam.LMX_CONNECT_DEVICE_INFO();
            uint ret;

            if (this.driver != null) {
                try {
                    uint GotDevices = LumixCam.LMX_func_api_Get_PnPDeviceInfo(ref devInfo, out retError);
                    uint countDevices = devInfo.find_PnpDevice_Count;

                    for (int i = 0; i < countDevices; i++) {
                        devices.Add(new LumixcameraDriver(profileService, exposureDataFactory, devInfo.find_PnpDevice_Info[i], devInfo, i));
                    }

                    Logger.Info($"Found {countDevices} Lumix Cameras");
                } catch (Exception ex) {
                    Logger.Error(ex);
                }
            }
            return devices;
        }
    }
}