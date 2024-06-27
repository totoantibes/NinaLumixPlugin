using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NINA.Core.Utility;
using System.Buffers;
using System.Collections;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Roberthasson.NINA.Lumixcamera.LumixPluginNative {

    internal class LumixCameraDLL {
        public const byte LMX_BOOL_TRUE = 1;
        public const int DEVINFO_DEF_ARRAY_MAX = 512; // Adjust based on the actual value in the SDK

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LMX_DEV_INFO {
            public uint DeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DeviceName;

            // Add other fields as necessary
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct LMX_CONNECT_DEVICE_INFO {
            public uint find_PnpDevice_Count;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = DEVINFO_DEF_ARRAY_MAX)]
            public IntPtr[] find_PnpDevice_IDs; // Array of PWSTR

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = DEVINFO_DEF_ARRAY_MAX)]
            public LMX_DEV_INFO[] find_PnpDevice_Info;
        }

        // P/Invoke signatures
        [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void LMX_func_api_Init();

        [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern byte LMX_func_api_Get_PnPDeviceInfo(ref LMX_CONNECT_DEVICE_INFO plmxPnpDevInfo);

        [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern byte LMX_func_api_Select_PnPDevice(uint dwDevIndex, ref LMX_CONNECT_DEVICE_INFO plmxPnpDevInfo);

        [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern byte LMX_func_api_Close_Device();

        [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern byte LMX_func_api_Open_Session(uint ulConnectVer, out uint pulDeviceConnectVer);

        [DllImport("lmxptpif.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern byte LMX_func_api_Close_Session();
    }
}