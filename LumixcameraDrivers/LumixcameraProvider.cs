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

public static class PanasonicSDK {

    // Define the LMX_BOOL_TRUE constant
    public const byte LMX_BOOL_TRUE = 1;

    // Define the LMX_CONNECT_DEVICE_INFO structure
    [StructLayout(LayoutKind.Sequential)]
    public struct LMX_CONNECT_DEVICE_INFO {

        // Define the fields as per the SDK documentation
        // For example:
        public int DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string DeviceName;

        // Add other fields as necessary
    }

    // Initialize the communication library
    [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern void LMX_func_api_Init();

    // Get information on devices that can be connected
    [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte LMX_func_api_Get_PnPDeviceInfo(ref LMX_CONNECT_DEVICE_INFO plmxPnpDevInfo);

    // Connect to a selected device
    [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte LMX_func_api_Select_PnPDevice(uint dwDevIndex, ref LMX_CONNECT_DEVICE_INFO plmxPnpDevInfo);

    // Disconnect a connected device
    [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte LMX_func_api_Close_Device();

    // Open a session to use USB
    [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte LMX_func_api_Open_Session(uint ulConnectVer, out uint pulDeviceConnectVer);

    // Close a session
    [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern byte LMX_func_api_Close_Session();
}

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

        [ImportingConstructor]
        public LumixcameraProvider(IProfileService profileService, IExposureDataFactory exposureDataFactory) {
            this.profileService = profileService;
            this.exposureDataFactory = exposureDataFactory;
        }

        public string Name => "LumixCameraPlugin";

        public IList<ICamera> GetEquipment() {
            var devices = new List<ICamera>();
            devices.Add(new LumixcameraDriver(profileService, exposureDataFactory));

            return devices;
        }
    }
}