/////////////////////////////////////////////////////
// 
// (C) 2020 Panasonic Corporation
// 
// Lumix USB PTP library related header
// 
// I/F function related to enable PTP command processing via USB
// 
// 
/////////////////////////////////////////////////////////////////////
// 
// DLL internal function
// 


#ifndef _LMX_FUNC_API_H_
#define _LMX_FUNC_API_H_



#include "Lmx_lib_type.h"
#include "LMX_lib_def.h"
#include "LMX_lib_dev_common.h"





/////////////////////////////////////////////////////////////////////
// 
// Func     :LMX_func_api_Init
// 
// Summ.    :Initialize module
// Input    :
//
// Output   :None
// 
// Remarks  :Please call this function first when starting the application
// 
// 

void  LMX_func_api_Init(void);


/////////////////////////////////////////////////////////////////////
// 
// Func     :LMX_func_api_Close_Device
// 
// Summ.    :Resource release of module
// Input    :None
//
// Output   :UINT8 
// 
// Remarks  :
// 
// 
UINT8  LMX_func_api_Close_Device(
	UINT32				*retError = NULL
);

/////////////////////////////////////////////////////////////////////
// 
// Func     :LMX_func_api_Get_PnPDeviceInfo
// 
// Summ.    :Get PnP connected device information (for WPD)
// Input    :
// 	        PLMX_CONNECT_DEVICE_INFO        plmxPnpDevInfo;
//
// Output   :
//          UINT8       LMX_BOOL_TRUE :Acquisition success
//                      LMX_BOOL_FALSE:Acquisition failure
// 
UINT8  LMX_func_api_Get_PnPDeviceInfo(
	PLMX_CONNECT_DEVICE_INFO plmxPnpDevInfo,
	UINT32				*retError = NULL
);

/////////////////////////////////////////////////////////////////////
// 
// Func     :LMX_func_api_Select_PnPDevice
// 
// Summ.    :Select the specified device from the PnP connected device (for WPD)
// Input    :
//          UINT32 dwTargetIndex
// 	        PLMX_CONNECT_DEVICE_INFO        plmxPnpDevInfo;
//
// Output   :
//          UINT8       LMX_BOOL_TRUE :Acquisition success
//                      LMX_BOOL_FALSE:Acquisition failure
// 
UINT8  LMX_func_api_Select_PnPDevice(
	UINT32 dwDevIndex,
	PLMX_CONNECT_DEVICE_INFO plmxPnpDevInfo,
	UINT32				*retError = NULL
);



/////////////////////////////////////////////////////////////////////
// 
//Callback function related
// 
// 
///////////////////////////////////////////////////////////////////////////////////////////////////
//
// Handle to notify the application and message registration (registration)
// 
//--- Function definition for CallBack registration  ---//

typedef int (*LMX_CALLBACK_FUNC)(UINT32, UINT32);


//--- Function for registering callback function ---//
UINT32  LMX_func_api_Reg_NotifyCallback(UINT32 CallBackType,LMX_CALLBACK_FUNC appfunc);

//--- Callback function registration deletion function ---//
UINT32  LMX_func_api_Delete_CallBackInfo(UINT32 CallBackType);


////////////////////////////////////////////////////////////////////
//
// ISO
//
UINT8  LMX_func_api_ISO_Get_Capability(LMX_STRUCT_ISO_CAPA_INFO* pIsoCapaInfo, UINT32 *retError = NULL);
UINT8  LMX_func_api_ISO_Get_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_ISO_Set_Param(UINT32 ulParam, UINT32 *retError = NULL);

UINT8  LMX_func_api_ISO_Get_UpperLimit(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_ISO_Set_UpperLimit(UINT32 ulParam, UINT32 *retError = NULL);


////////////////////////////////////////////////////////////////////
//
// ShutterSpeed
//
UINT8  LMX_func_api_SS_Get_Capability(LMX_STRUCT_SS_CAPA_INFO* pSS_CapaInfo, UINT32 *retError = NULL);
UINT8  LMX_func_api_SS_Get_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_SS_Set_Param(UINT32 ulParam, UINT32 *retError = NULL);

UINT8  LMX_func_api_SS_Get_RangeLimit(UINT32 *pulMinParam, UINT32* pulMaxParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_SS_Set_RangeLimit(UINT32 ulMinParam, UINT32 ulMaxParam, UINT32 *retError = NULL);

////////////////////////////////////////////////////////////////////
//
// WB
//
UINT8  LMX_func_api_WB_Get_Capability(LMX_STRUCT_WB_CAPA_INFO*	pWB_CapaInfo, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Set_Param(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Set_KSet(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Set_ADJ_AB(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Set_ADJ_GM(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Set_ADJ_AB_Sep(UINT32 ulParam, UINT32 *retError = NULL);

UINT8  LMX_func_api_WB_Get_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Get_KSet(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Get_ADJ_AB(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Get_ADJ_GM(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_WB_Get_ADJ_AB_Sep(UINT32 *pulParam, UINT32 *retError = NULL);

////////////////////////////////////////////////////////////////////
//
// Aperture
//
UINT8  LMX_func_api_Aperture_Get_Capability(LMX_STRUCT_APERTURE_CAPA_INFO*	pAperture_CapaInfo, UINT32 *retError = NULL);
UINT8  LMX_func_api_Aperture_Get_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Aperture_Get_RangeLimit(UINT32 *pulMinParam,UINT32* pulMaxParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Aperture_Set_Param(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Aperture_Set_RangeLimit(UINT32 ulMinParam, UINT32 ulMaxParam, UINT32 *retError = NULL);

////////////////////////////////////////////////////////////////////
//
// AF Config (Area/Mode)
//
UINT8  LMX_func_api_AF_Config_Get_Capability(LMX_STRUCT_AF_CONFIG_CAPA_INFO* pAF_Cfg_CapaInfo, UINT32 *retError = NULL);
UINT8  LMX_func_api_AF_Config_Get_AF_Mode_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_AF_Config_Get_AF_Area_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_AF_Config_Set_AF_Mode_Param(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_AF_Config_Set_AF_Area_Param(UINT32 ulParam, UINT32 *retError = NULL);

UINT8  LMX_func_api_AF_Config_Get_Quick_AF_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_AF_Config_Set_Quick_AF_Param(UINT32 ulParam, UINT32 *retError = NULL);


////////////////////////////////////////////////////////////////////
//
// Exposure
//
UINT8  LMX_func_api_Exposure_Get_Capability(LMX_STRUCT_EXPOSURE_CAPA_INFO* pExposure_CapaInfo, UINT32 *retError = NULL);
UINT8  LMX_func_api_Exposure_Get_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Exposure_Get_RangeLimit(UINT32 *pulMinParam,UINT32* pulMaxParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Exposure_Set_Param(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Exposure_Set_RangeLimit(UINT32 ulMinParam, UINT32 ulMaxParam, UINT32 *retError = NULL);

////////////////////////////////////////////////////////////////////
//
// Camera Mode Info
//
UINT8  LMX_func_api_CameraMode_Get_Capability(
	LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO*				pCameraMode_CapaInfo,				
	UINT32 *retError = NULL
);
UINT8  LMX_func_api_CameraMode_Get_DriveMode(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_CameraMode_Get_Mode_Pos(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_CameraMode_Get_CreativeMode(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_CameraMode_Get_iA_Mode(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_CameraMode_Set_DriveMode(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_CameraMode_Set_Mode_Pos(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_CameraMode_Set_CreatvieMode(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_CameraMode_Set_iA_Mode(UINT32 ulParam, UINT32 *retError = NULL);



////////////////////////////////////////////////////////////////////
//
// Movie Config
//
UINT8  LMX_func_api_Mov_Config_Get_Capability(LMX_STRUCT_MOV_MENU_CONFIG_CAPA_INFO*	pMov_Cfg_CapaInfo, UINT32 *retError = NULL);
UINT8  LMX_func_api_Mov_Config_Get_C_Movie_mode_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Mov_Config_Get_HDMI_mode_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Mov_Config_Get_Quality_mode_Param(UINT32 *pulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Mov_Config_Get_Rec_mode_Param(UINT32 *pulParam, UINT32 *retError = NULL);

UINT8  LMX_func_api_Mov_Config_Set_C_Movie_mode_Param(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Mov_Config_Set_HDMI_mode_Param(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Mov_Config_Set_Quality_mode_Param(UINT32 ulParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_Mov_Config_Set_Rec_mode_Param(UINT32 ulParam, UINT32 *retError = NULL);


UINT8  LMX_func_api_SetupFilesConfig_Get_Target(UINT16 *punParam, UINT32 *retError = NULL);
UINT8  LMX_func_api_SetupFilesConfig_Set_Target(UINT16 	unParam, UINT32 *retError = NULL);


////////////////////////////////////////////////////////////////////
//
// Execution system: Photographing system
//
UINT8  LMX_func_api_Rec_Ctrl_Release(LMX_STRUCT_REC_CTRL* lpRecCtrl, UINT32 *retError = NULL);
UINT8  LMX_func_api_Rec_Ctrl_AF_AE(LMX_STRUCT_REC_CTRL* lpRecCtrl, UINT32 *retError = NULL);


UINT8  LMX_func_api_Rec_Ctrl_Zoom(LMX_STRUCT_REC_CTRL* lpRecCtrl, UINT32 *retError = NULL);


UINT8  LMX_func_api_Rec_Ctrl_Lens(LMX_STRUCT_REC_CTRL* lpRecCtrl, UINT32 *retError = NULL);


////////////////////////////////////////////////////////////////////
//
// Executing system: Photographing system (moving image)
//
UINT8  LMX_func_api_MoveRec_Ctrl_Start(UINT32 *retError = NULL);
UINT8  LMX_func_api_MoveRec_Ctrl_Stop(UINT8 stopmode, UINT32 *retError = NULL);


////////////////////////////////////////////////////////////////////
//
// Object system:
//
UINT8  LMX_func_api_Get_Object(UINT32		ObjectHandle,UINT8*		lpStoreBufAdder,UINT32		StoreBufSize, UINT32 *retError = NULL);
UINT8  LMX_func_api_Get_Object_FormatType(UINT32 ObjHandle,UINT32* pFormatType, UINT32 *retError = NULL);


UINT8  LMX_func_api_Get_Object_DataSize(UINT32 ObjHandle,UINT64* pDataSize, UINT32 *retError = NULL);


UINT8  LMX_func_api_Get_Object_FileName(UINT32 ObjHandle,LMX_STRUCT_PTP_ARRAY_STRING* pFileName, UINT32 *retError = NULL);
UINT8  LMX_func_api_Get_Partial_Object(
	UINT32		ObjectHandle,
	UINT8*		lpStoreBufAdder,
	UINT32		ui32_DataOffset,
	UINT32		u32_SplitTransferBytes,
	UINT32		*retError = NULL
);
UINT8  LMX_func_api_Get_Partial_ObjectEx(
	UINT32		ObjectHandle,
	UINT8*		lpStoreBufAdder,
	UINT64		ui64_DataOffset,
	UINT32		u32_SplitTransferBytes,
	UINT32		*retError = NULL
);

UINT8  LMX_func_api_Skip_Object_Transfer(
    UINT32		ObjectHandle,
	UINT32		*retError = NULL
);


////////////////////////////////////////////////////////////////////
//
// LiveView system:
//
UINT8  LMX_func_api_Ctrl_LiveView_Start(UINT32 *retError = NULL);
UINT8  LMX_func_api_Ctrl_LiveView_Stop(UINT32 *retError = NULL);
UINT8  LMX_func_api_Get_LiveView_data(
	LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM*	pHistBuf,
	UINT32*         pHistSize,
	LMX_STRUCT_LIVEVIEW_INFO_POSTURE*	pPostBuf,
	UINT32*         pPostSize,
	LMX_STRUCT_LIVEVIEW_INFO_LEVEL*	pLevelBuf,
	UINT32*         pLevelSize,
	UINT8* 		pJpegBuf,
	UINT32*         pJpegSize,
	UINT32 *retError = NULL
);



////////////////////////////////////////////////////////////////////
//
// --- LmxOpenSession/LmxCloseSession ---//
//
UINT8  LMX_func_api_Open_Session(UINT32 ulConnectVer,UINT32* pulDeviceConnectVer, UINT32 *retError = NULL);
UINT8  LMX_func_api_Close_Session(UINT32 *retError = NULL);


////////////////////////////////////////////////////////////////////
//
// LIVEVIEW STREAM
//
UINT8  LMX_func_api_LiveView_Str_Get_Trans_Img(LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG *pTransImg, UINT32 *retError = NULL);
UINT8  LMX_func_api_LiveView_Str_Set_Trans_Img(LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG pTransImg, UINT32 *retError = NULL);
UINT8  LMX_func_api_LiveView_Str_Get_Recom_Img(LMX_STRUCT_LIVEVIEW_STR_RECOM_IMG *pRecomImg, UINT32 *retError = NULL);


#endif //_LMX_FUNC_API_H_
