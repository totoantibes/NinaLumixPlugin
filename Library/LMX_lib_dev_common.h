/////////////////////////////////////////////////////
// 
// (C) 2020 Panasonic Corporation
//
// PnP device detection, selection API definition
// 
// PnP device detection, selection API definition: "LMX_lib_dev_common.h"
// 
// Å¶ Definition name
// 
// 
// 
// 
// 
// 
/////////////////////////////////////////////////////////////////////
// 
// Definition necessary for PTP IF (common)
// 
#ifndef _LMX_LIB_DEV_COMMON_H_
#define _LMX_LIB_DEV_COMMON_H_

#include "Lmx_lib_type.h"

//--- Definition setting required by PTP ---//
#define DEVINFO_DEF_ARRAY_MAX	512		// The number of ARRAY (other than String) is represented by ULONG, but in this application it is possible to represent up to 512 //
#define DEVINFO_DEF_STRING_MAX	256		// Since ARRAY (String only) can be represented by UCHAR, we can express up to 256 expressed in UCHAR //


typedef struct _tag_LMX_DEVINFO{
	UINT32 dev_Index;
	WCHAR dev_MakerName[DEVINFO_DEF_STRING_MAX];	// Maker Name
	UINT32 dev_MakerName_Length;
	WCHAR dev_ModelName[DEVINFO_DEF_STRING_MAX];	// Model Name
	UINT32 dev_ModelName_Length;
}LMX_DEV_INFO,*PLMX_DEV_INFO;

typedef struct _tag_LMX_CONNECT_DEVICE_INFO{
	UINT32			find_PnpDevice_Count;							// Number of devices detected //
	PWSTR			find_PnpDevice_IDs[DEVINFO_DEF_ARRAY_MAX];
	LMX_DEV_INFO	find_PnpDevice_Info[DEVINFO_DEF_ARRAY_MAX];	    // Detected device information: Maximum DEVINFO_DEF_STRING_MAX //
}LMX_CONNECT_DEVICE_INFO,*PLMX_CONNECT_DEVICE_INFO;


#endif // #define _LMX_LIB_DEV_COMMON_H_


