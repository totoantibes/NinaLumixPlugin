/////////////////////////////////////////////////////////////////////
// 
// (C) 2020 Panasonic Corporation
// 
// LMX_lib_def.h 
// 
// Definition necessary to use Lmxptpif.DLL
// 
/////////////////////////////////////////////////////////////////////

#include "Lmx_lib_type.h"


#ifndef _LMX_LIB_DEF_H__
#define _LMX_LIB_DEF_H__


#pragma pack(push, Old, 1)




//////////////////////////////////////////////////////////////////////////////////////////////////
// 																								//
// Error definition																					//
// 																								//
//////////////////////////////////////////////////////////////////////////////////////////////////
enum Lmx_def_error_code {
	LMX_DEF_ERR_NO_ERROR			= 0x00000000,	// No error
	
	LMX_DEF_ERR_FUNC_PARAM			= 0x00010000,	// Function Argument Related: Argument error
	LMX_DEF_ERR_FUNC_UNKNOWN,						// Function Argument Related: Other error
	
	LMX_DEF_ERR_CB_INVALID_ID		= 0x00020000,	// Callback Related: Invalid ID
	LMX_DEF_ERR_CB_INVALID_FUNC,					// Callback Related: Function invalid
	LMX_DEF_ERR_CB_INVALID_PARAM,					// Callback Related: Illegal parameters
	LMX_DEF_ERR_CB_SAME_ID,							// Callback Related: Registered with the same ID (function is different) 
	LMX_DEF_ERR_CB_LIMIT,							// Callback Related: Registration limit
	LMX_DEF_ERR_CB_NOT_FIND,						// Callback Related: Specified ID not found
	LMX_DEF_ERR_CB_UNKNOWN,							// Callback Related: Other error

	LMX_DEF_ERR_DEV_DETECT			= 0x00030000,	// Device Selection Related: Detection error 
	LMX_DEF_ERR_DEV_OPEN,							// Device Selection Related: Selected device open error 
	LMX_DEF_ERR_DEV_NEED_OPEN,						// Device Selection Related: Not open or disconnected
	LMX_DEF_ERR_DEV_UNKNOWN,						// Device Selection Related: Other error
	
	LMX_DEF_ERR_COM_INVALID_PARAM	= 0x00040000,	// Data Transmission Related: Parameter error 
	LMX_DEF_ERR_COM_CMD,							// Data Transmission Related: Command transmission error 
	LMX_DEF_ERR_COM_DATA_SEND,						// Data Transmission Related: Data transmission error 
	LMX_DEF_ERR_COM_DATA_RCV,						// Data Transmission Related: Data reception error 
	LMX_DEF_ERR_COM_DATA_BUSY,
	LMX_DEF_ERR_COM_RES,							// Data Transmission Related: Response error 
	LMX_DEF_ERR_COM_MEM_ADD,						// Data Transmission Related: Memory allocation error
	LMX_DEF_ERR_COM_UNKNOWN,						// Data Transmission Related: Other error
	LMX_DEF_ERR_COM_THREAD,							// Data Transmission Related: Thread creation failure error
	LMX_DEF_ERR_COM_TIMEOUT,						// Data Transmission Related: Communication timeout error
	LMX_DEF_ERR_COM_TIMEOUT_RECONNECT_OK,			// Data Transmission Related: Reconnect OK
	LMX_DEF_ERR_COM_TIMEOUT_RECONNECT_ERROR,		// Data Transmission Related: Reconnect ERROR

	
	LMX_DEF_ERR_COM_SESSION_ALREADY_OPENED=0x00041000,		// Session: ERROR already open
	LMX_DEF_ERR_COM_SESSION_NOT_OPENED,						// Session: Not opened yet
	LMX_DEF_ERR_COM_SESSION_NOT_SUPPORT,					// Session: Not supported: Command
	LMX_DEF_ERR_COM_SESSION_NOT_SUPPORT_VERSION,			// Session: Not supported: Version


	LMX_DEF_ERR_EVENT_RCV_UNKNOWN	= 0x00050000,	// Event Notification Related: Unexpected event reception error 		
	LMX_DEF_ERR_EVENT_WAIT_TIMEOUT,					// Event Notification Related: Event wait timeout error 		

	LMX_DEF_ERR_FILE				= 0x00060000,	// File System Related: File system general error 
	LMX_DEF_ERR_FILE_PATH_LEN,						// File System Related: File path length error
	LMX_DEF_ERR_FILE_OPEN,							// File System Related: File open error
	LMX_DEF_ERR_FILE_SIZE,							// File System Related: File size error
	LMX_DEF_ERR_FILE_READ,							// File System Related: File reading error
	LMX_DEF_ERR_FILE_TYPE_FWUP_UNKNOWN,				// File System Related: File format unknown (FWUP)

	LMX_DEF_ERR_MEM					= 0x00070000,	// Memory System Related: General error
	LMX_DEF_ERR_MEM_CREATE,							// Memory System Related: Creating error

	LMX_DEF_ERR_INTERNAL			= 0x000F0000,	// Internal Error: 
	LMX_DEF_ERR_INTERNAL_EXCEPTION,					// Internal Error:Exception(Including Windows AV)
	LMX_DEF_ERR_DEV_FWUP_NOTREADY   ,				// Internal Error:FWUP preparation error (battery shortage etc.)
	LMX_DEF_ERR_DEV_FWUP_ERROR,						// Internal Error:FWUP preparation error (battery shortage etc.)
	LMX_DEF_ERR_DEV_FWUP_ERROR_VERSION,				// Internal Error:FWUP preparation error (firmware up complete: failure: version)

	LMX_DEF_ERR_CAM					= 0x00100000,	// Camera Command Error:
	LMX_DEF_ERR_CAM_INVALID_MODE,					// Camera Command Error: Invalid mode error

	LMX_DEF_ERR_MAX									// Error: Other

};


//////////////////////////////////////////////////////////////////////////////////////////////////
// 																								//
// Event definition/callback ID definition														//
// 																								//
//////////////////////////////////////////////////////////////////////////////////////////////////
enum Lmx_event_id : UINT32 {
	LMX_DEF_LIB_EVENT_ID_ISO					= 0x02000020,						// Event/Callback registration ID:ISO information  
	LMX_DEF_LIB_EVENT_ID_SHUTTER				= 0x02000030,						// Event/Callback registration ID:ShutterSpeed information
	LMX_DEF_LIB_EVENT_ID_APERTURE				= 0x02000040,						// Event/Callback registration ID:Apertuer information 
	LMX_DEF_LIB_EVENT_ID_WHITEBALANCE			= 0x02000050,						// Event/Callback registration ID:WhiteBalance information 
	LMX_DEF_LIB_EVENT_ID_EXPOSURE				= 0x02000060,						// Event/Callback registration ID:Exposure 
	LMX_DEF_LIB_EVENT_ID_AF_CONFIG				= 0x02000070,						// Event/Callback registration ID:AF mode/AF area


	LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE		= 0x03000010,						// Event/Callback registration ID:Shooting operation 

	LMX_DEF_LIB_EVENT_ID_REC_CTRL_AFAE			= 0x03000020,						// Event/Callback registration ID:Shooting operation 


	LMX_DEF_LIB_EVENT_ID_REC_CTRL_ZOOM			= 0x03000080,						// Event/Callback registration ID:Shooting operation 


	LMX_DEF_LIB_EVENT_ID_REC_CTRL_LENS			= 0x03010010,						// Event/Callback registration ID:Lens operation


	LMX_DEF_LIB_EVENT_ID_OBJCT_ADD				= 0x10000040,						// Event/Callback registration ID:Object related notification:Add object 

		
	LMX_DEF_LIB_EVENT_ID_OBJCT_REQ_TRNSFER		= 0x10000043,						// Event/Callback registration ID:Object related notification:Transfer request 

};


//////////////////////////////////////////////////////////////////////////////////////////////////
// 																								//
// OBJECT system:Format returned by Lmx_func_api_Get_Object_FormatType		        			//
// 																								//
//////////////////////////////////////////////////////////////////////////////////////////////////
enum Lmx_def_lib_object_format {
    LMX_DEF_OBJ_FORMAT_UNKNOWN = 0,
    LMX_DEF_OBJ_FORMAT_JPEG,
    LMX_DEF_OBJ_FORMAT_RAW,
    LMX_DEF_OBJ_FORMAT_FOLDER,
    LMX_DEF_OBJ_FORMAT_MOVIE_MOV,
    LMX_DEF_OBJ_FORMAT_MOVIE_MP4,
	LMX_DEF_OBJ_FORMAT_HLG,
    LMX_DEF_OBJ_FORMAT_MAX
};
#define LMX_DEF_OBJ_CARDLESS_TRNSFER_HDL	0x12345678


//////////////////////////////////////////////////////////////////////////////////////////////////
// 																								//
// REC information related definition: ISO value definition 									//
// 																								//
//////////////////////////////////////////////////////////////////////////////////////////////////
enum Lmx_def_lib_ISO_param {
    LMX_DEF_ISO_UNKNOWN = 0xFFFFFFFD,               // ISO Unknown		
    LMX_DEF_ISO_I_ISO = 0xFFFFFFFE,                 // i_ISO		
    LMX_DEF_ISO_AUTO = 0xFFFFFFFF,                  // ISO Auto	
};

//////////////////////////////////////////////////////////////////////////////////////////////////
// 																								//
// REC information related definition: ISO upper limit setting definition						//
// 																								//
//////////////////////////////////////////////////////////////////////////////////////////////////
enum Lmx_def_lib_ISO_Upper_limit {
    LMX_DEF_ISO_UPPER_LIMIT_OFF = 0x0000,					// OFF
};


/////////////////////////////////////////////////
//
// RecInfo: ISO information (Capability) storage structure
//
/////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_ISO_CAPA_INFO {
//	UINT16								CapaType;		// Range or  Eenum //
    LMX_STRUCT_PTP_FORM_ENUM_UINT32		Capa_Enum;		// EnumType  Data  //
    LMX_STRUCT_PTP_FORM_RANGE_UINT32	Capa_Range;		// RangeType Data  //
    UINT32								CurVal;			// CurrentValue    //
}LMX_STRUCT_ISO_CAPA_INFO;


////////////////////////////////////////////////////////////
// DevicePropCode : 0xD001	Lumix Extension Exposure Time //
////////////////////////////////////////////////////////////
enum Lmx_def_lib_DevpropEx_ShutterSpeed_param {
    LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_BULB     = 0xFFFFFFFF,		// Bulb		//
    LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_UNKNOWN  = 0x0FFFFFFE,		//  SS unknown	//
    LMX_DEF_PTP_DEVPROP_EXT_LMX_SS_AUTO     = 0x0FFFFFFF,		//  SS Auto	//
};


/////////////////////////////////////////////////
//
// RecInfo: SS information (All) storage structure 
//
/////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_SS_CAPA_INFO {
//	UINT16								CapaType;		// Range or  Eenum //
    LMX_STRUCT_PTP_FORM_ENUM_UINT32		Capa_Enum;		// EnumType  Data  //
    LMX_STRUCT_PTP_FORM_RANGE_UINT32	CurVal_Range;	// RangeType Data  //
    UINT32								CurVal;			// CurrentValue    //
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_Cfg;	// EnumType  Data  //
    UINT16								CurVal_Cfg;		// CurrentValue    //
}LMX_STRUCT_SS_CAPA_INFO;


//////////////////////////////////////////////////////////////////////////////////////////////////
// 																								//
// REC information related definition: APERTURE value definition    							//
// 																								//
//////////////////////////////////////////////////////////////////////////////////////////////////
enum Lmx_def_lib_Aperture_param {
    LMX_DEF_F_UNKNOWN = 0xFFFE,
    LMX_DEF_F_AUTO = 0xFFFF,
};


/////////////////////////////////////////////////
//
// RecInfo:Aperture information (All) storage structure 
//
/////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_APERTURE_CAPA_INFO {							
    UINT16								CurVal;
    LMX_STRUCT_PTP_FORM_RANGE_UINT16	CurVal_Range;
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum;
}LMX_STRUCT_APERTURE_CAPA_INFO;


/////////////////////////////////////////////////
//
// WB:WB Value 
//
////////////////////////////////////////////////
enum Lmx_def_lib_WhiteBalance_param {
    LMX_DEF_WB_AUTO         = 0x0002,	// WhilteBalance:Auto
    LMX_DEF_WB_DAYLIGHT     = 0x0004,	// WhilteBalance:Day light
    LMX_DEF_WB_CLOUD        = 0x8008,	// WhilteBalance:Cloud
    LMX_DEF_WB_TENGSTEN     = 0x0006,	// WhilteBalance:Incandescent 
    LMX_DEF_WB_WHITESET     = 0x8009,	// WhilteBalance:White set
    LMX_DEF_WB_FLASH        = 0x0007,	// WhilteBalance:Flash
    LMX_DEF_WB_FLUORESCENT  = 0x0005,	// WhilteBalance:Flourescent
    LMX_DEF_WB_BLACK_WHITE  = 0x800A,	// WhilteBalance:Black white
    LMX_DEF_WB_KEEP         = 0x800B,	// WhilteBalance:WB setting 1
    LMX_DEF_WB_KEEP2        = 0x800C,	// WhilteBalance:WB setting 2
    LMX_DEF_WB_KEEP3        = 0x800D,	// WhilteBalance:WB setting 3
    LMX_DEF_WB_KEEP4        = 0x800E,	// WhilteBalance:WB setting 4
    LMX_DEF_WB_SHADE        = 0x800F,	// WhilteBalance:Shade
    LMX_DEF_WB_K_SET        = 0x8010,	// WhilteBalance:Color temperature
    LMX_DEF_WB_K_SET2       = 0x8011,	// WhilteBalance:Color temperature 2
    LMX_DEF_WB_K_SET3       = 0x8012,	// WhilteBalance:Color temperature 3
    LMX_DEF_WB_K_SET4       = 0x8013,	// WhilteBalance:Color temperature 4
    LMX_DEF_WB_AUTO_COOL	= 0x8014,	// WhilteBalance:Auto cool
	LMX_DEF_WB_AUTO_WARM   = 0x8015,	// WhilteBalance:Auto warm
};


/////////////////////////////////////////////////
//
// RecInfo: WB information (Capability) storage structure
//
/////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_WB_CAPA_INFO {
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_WB;
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_WB_K_Set;
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_WB_AB;
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_WB_GM;
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_WB_AB_Sep;
    UINT16								CurVal_WB;				// CurrentValue    //
    UINT16								CurVal_WB_K_Set;		// CurrentValue    //
    UINT16								CurVal_ADJ_AB;			// CurrentValue    //
    UINT16								CurVal_ADJ_GM;			// CurrentValue    //
    UINT16								CurVal_ADJ_AB_Sep;		// CurrentValue    //
}LMX_STRUCT_WB_CAPA_INFO;


/////////////////////////////////////////////////
//
// EXPOSURE:EXPOSURE capability information storage structure 
//
/////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_EXPOSURE_CAPA_INFO {					// Exposure information 
    UINT16								CurVal;						// Exposure value
    LMX_STRUCT_PTP_FORM_RANGE_UINT16	CurVal_Range;				// Exposure Range value
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum;					// Exposure List of values
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_Upper;			// Exposure Upper limit values
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_Lower;			// Exposure Lower limit values
}LMX_STRUCT_EXPOSURE_CAPA_INFO;


/////////////////////////////////////////////////
//
// AF Config information related definition: AF Mode related definition
//
////////////////////////////////////////////////
enum Lmx_def_lib_AF_Config_AF_mode {
    LMX_DEF_AFCONFIG_AF_MODE_AF = 0,
    LMX_DEF_AFCONFIG_AF_MODE_AF_MACRO,
    LMX_DEF_AFCONFIG_AF_MODE_AF_MACRO_DIGITAL,
    LMX_DEF_AFCONFIG_AF_MODE_MF,
    LMX_DEF_AFCONFIG_AF_MODE_AF_S,
    LMX_DEF_AFCONFIG_AF_MODE_AF_C,
    LMX_DEF_AFCONFIG_AF_MODE_AF_F,
    LMX_DEF_AFCONFIG_AF_MODE_MAX
};

/////////////////////////////////////////////////
//
// AF Config information related definition: AF Area related definition
//
////////////////////////////////////////////////
enum Lmx_def_lib_AF_Config_AF_area {
    LMX_DEF_AFCONFIG_AF_AREA_1POINT = 0,			// AF Config:AF area:1 point
    LMX_DEF_AFCONFIG_AF_AREA_FACE,					// AF Config:AF area:Face recognition 
    LMX_DEF_AFCONFIG_AF_AREA_TRACKING,				// AF Config:AF area:Tracking
    LMX_DEF_AFCONFIG_AF_AREA_PIN_POINT,				// AF Config:AF area:Pin point 
    LMX_DEF_AFCONFIG_AF_AREA_49_POINT,				// AF Config:AF area:49 points
    LMX_DEF_AFCONFIG_AF_AREA_CUSTOM_MULTI,			// AF Config:AF area:Custom multi
    LMX_DEF_AFCONFIG_AF_AREA_225_POINT_AF,			// AF Config:AF area:225 points
    LMX_DEF_AFCONFIG_AF_AREA_9_POINT_AF,			// AF Config:AF area:9 points
    LMX_DEF_AFCONFIG_AF_AREA_5_POINT_AF,			// AF Config:AF area:5 points
    LMX_DEF_AFCONFIG_AF_AREA_3_POINT_AF_HS,			// AF Config:AF area:3 points AF (high speed)
    LMX_DEF_AFCONFIG_AF_AREA_3_POINT_AF,			// AF Config:AF area:3 points
    LMX_DEF_AFCONFIG_AF_AREA_1_POINT_AF_HS,			// AF Config:AF area:1 points (high speed)
    LMX_DEF_AFCONFIG_AF_AREA_TOUCH_AF,				// AF Config:AF area:Touch AF
    LMX_DEF_AFCONFIG_AF_AREA_TOUCH_AF_FREE,			// AF Config:AF area:Touch AF (release)
	LMX_DEF_AFCONFIG_AF_AREA_1POINT_AUXILIARY,		// AF Config:AF area:1ì_ + ï‚èï
	LMX_DEF_AFCONFIG_AF_AREA_VERTICAL_HORIZONTAL,	// AF Config:AF area:ècÅEâ°
	LMX_DEF_AFCONFIG_AF_AREA_SQUARE,				// AF Config:AF area:éläp
	LMX_DEF_AFCONFIG_AF_AREA_ELLIPSE,				// AF Config:AF area:ë»â~
	LMX_DEF_AFCONFIG_AF_AREA_NEW_CUSTOM_1,			// AF Config:AF area:Custom 1
	LMX_DEF_AFCONFIG_AF_AREA_NEW_CUSTOM_2,			// AF Config:AF area:Custom 2
	LMX_DEF_AFCONFIG_AF_AREA_NEW_CUSTOM_3,			// AF Config:AF area:Custom 3
};

/////////////////////////////////////////////////
//
// AF Config information related definition: Quick AF related definition
//
////////////////////////////////////////////////
enum Lmx_def_lib_AF_Config_Quick_AF {
    LMX_DEF_AFCONFIG_QUICK_AF_OFF = 0,				// AF Config:QuickAF:OFF 
    LMX_DEF_AFCONFIG_QUICK_AF_PAF = 3,				// AF Config:QuickAF:P-AF
};


/////////////////////////////////////////////////
//
// AF CONFIG: AF CONFIG Capability information storage structure
//
/////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_AF_CONFIG_CAPA_INFO {				// AF Config Capability information
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_AF_mode;			// AF mode information list
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_AF_area;			// AF Area information list
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_Quick_AF;			// Quick AF information list
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_AF_area_custom;	// AF area (custom) information list
    UINT16								CurVal_af_mode;				// AF mode value
    UINT16								CurVal_af_area;				// AF area value
    UINT16								CurVal_quick_af;			// Quick AF value
    UINT16								CurVal_af_area_custom;		// AF area (custom) value
}LMX_STRUCT_AF_CONFIG_CAPA_INFO;


/////////////////////////////////////////////////
//
// CAMERA MODE:Drive Mode setting
//
////////////////////////////////////////////////
enum Lmx_def_lib_Camera_Mode_Info_Drive_Mode{
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_SINGLE = 0,                 // DriveMode:Single shot 
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_BURST,						// DriveMode:Sequential shooting
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_BRACKET,					// DriveMode:Bracket
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_SELFTIMER,					// DriveMode:Self timer
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_INTERVAL_KOMADORI,		    // DriveMode:Interval
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_4K_PHOTO,					// DriveMode:4K Photo
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_FOCUS_SELECT,				// DriveMode:Focus select
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_BURST1,						// DriveMode:Continuous shooting 1
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_BURST2,						// DriveMode:Continuous shooting 2
	LMX_DEF_CAMERA_MODE_INFO_DRIVE_MODE_MAX,						// DriveMode:Reserved
};

/////////////////////////////////////////////////
//
// CAMERA MODE:Mode Dial setting
//
////////////////////////////////////////////////
enum Lmx_def_lib_Camera_Mode_Info_Mode_Pos{
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_P = 0,		                // MODE_POS:P mode
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_A,							// MODE_POS:A mode
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_S,							// MODE_POS:S mode
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_M,							// MODE_POS:M mode
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_COLOR,						// MODE_POS:Color
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_MOVREC,						// MODE_POS:Movie Rec
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_SCENE,						// MODE_POS:SCENE
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_AUTO,							// MODE_POS:Auto
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM,						// MODE_POS:Custom
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM2,						// MODE_POS:Custom
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_CUSTOM3,						// MODE_POS:Custom
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_PANORAMA,						// MODE_POS:Panorama
    LMX_DEF_CAMERA_MODE_INFO_MODE_POS_SQ,                           // MODE_POS:S&Q(Slow & Quick motion)
	LMX_DEF_CAMERA_MODE_INFO_MODE_POS_MAX							// MODE_POS:Reserved
};

/////////////////////////////////////////////////
//
// CAMERA MODE:Creative Mode setting
//
////////////////////////////////////////////////
enum Lmx_def_lib_Camera_Mode_Info_Creative_Mode{
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_POP = 0,			        // CreativeMode:POP
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_RETRO,					// CreativeMode:RETORO
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_HIGH_KEY,				// CreativeMode:HIGH KEY
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_LOW_KEY,					// CreativeMode:LOW KEY
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_SEPIA,					// CreativeMode:SEPIA
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_HIGH_DYNAMIC,			// CreativeMode:HIGH DYNAMIC
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_TOY_PHOTO,				// CreativeMode:TOY PHOTO
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_DIORAMA,					// CreativeMode:DIORAMA
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_SOFT_FOCUS,				// CreativeMode:SOFT FOCUS
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_DYNAMIC_MONOCHROME,		// CreativeMode:DYNAMIC MONOCHROME
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_IMPRESSIVE_ART,			// CreativeMode:IMPRESSIVE ART
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_MONOCHROME,		        // CreativeMode:MONOCHROME
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_CROSS_PROCESS,			// CreativeMode:CROSS PROCESS
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_ONEPOINT_COLOR,			// CreativeMode:ONEPOINT COLOR
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_CROSS_FILTER,			// CreativeMode:CROSS FILTER
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_EARLY_BIRD,				// CreativeMode:EARLY BIRD
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_MORNING_GLOW,			// CreativeMode:MORNING GLOW
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_BLEACH_BYPASS,			// CreativeMode:BLEACH BYPASS
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_LOMO_EFFECT,				// CreativeMode:LOMO EFFECT
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_DAY_DREAM,				// CreativeMode:DAY DREAM
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_SILKEY_MONOCHROME,		// CreativeMode:SILKEY MONOCHROME
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_ROUGH_MONOCHROME,		// CreativeMode:ROUGH MONOCHROME
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_OFF,						// CreativeMode:OFF
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_PINHOLE,					// CreativeMode:PINHOLE
	
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_PURE,					// CreativeMode:PURE
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_DYNAMIC_ART,				// CreativeMode:DYNAMIC ART
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_CHIC,					// CreativeMode:CHIC
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_BLACK_WHITE,				// CreativeMode:BLACK WHITE
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_SILHOUETTE,				// CreativeMode:SILHOUETTE
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_CINEMA,					// CreativeMode:CINEMA
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_CUSTOM,					// CreativeMode:CUSTOM
	LMX_DEF_CAMERA_MODE_INFO_CREATIVE_MODE_MAX						// CreativeMode:
};

/////////////////////////////////////////////////
//
// CAMERA MODE:iA Mode setting
//
////////////////////////////////////////////////
enum Lmx_def_lib_Camera_Mode_Info_iA_Mode{
	LMX_DEF_CAMERA_MODE_INFO_IA_MODE_AUTO = 0,				        // iA Mode:Auto
	LMX_DEF_CAMERA_MODE_INFO_IA_MODE_AUTO_PLUS,						// iA Mode:Auto+
	LMX_DEF_CAMERA_MODE_INFO_IA_MODE_MAX							// iA Mode:
};


/////////////////////////////////////////////////
//
// RecInfo: WB information (Capability) storage structure
//
/////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO{
	LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_mode_drive;		// EnumType Drive Mode      //
	LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_mode_pos;			// EnumType ModeDial Pos    //
	LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_mode_creative;	// EnumType Creatvie mode   //
	LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_mode_ia;			// EnumType iA mode         //
	LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_mode_4k;			// EnumType 4k mode         //
	LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_sceneguide_param;	// EnumType scene guide     //
	LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_Enum_c3_param;		    // EnumType c3              //
	UINT16								CurVal_mode_drive;			// CurrentValue    //
	UINT16								CurVal_mode_pos;			// CurrentValue    //
	UINT16								CurVal_mode_creative;		// CurrentValue    //
	UINT16								CurVal_mode_ia;				// CurrentValue    //
	UINT16								CurVal_mode_4k;				// CurrentValue    //
	UINT16								CurVal_sceneguide_param;	// CurrentValue    //
	UINT16								CurVal_c3_param;			// CurrentValue    //
}LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO;


/////////////////////////////////////////////////
// 
// Tag definition: shooting execution system: shooting system
// 
/////////////////////////////////////////////////
enum Lmx_TagID_Rec_Ctrl_Release {
    LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_RESRV = LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE,
    LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_ONESHOT = LMX_DEF_LIB_EVENT_ID_REC_CTRL_RELEASE + 1,


    LMX_DEF_LIB_TAG_REC_CTRL_RELEASE_MAX
};


//////////////////////////////////////////////////////////////////////////////////////////////////
// 																								//
// Shooting system: shooting execution system: shooting system definition					    //
// 																								//
//////////////////////////////////////////////////////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_REC_CTRL {
    UINT32							CtrlID;
    LMX_STRUCT_PTP_FORM_ENUM_UINT32 ParamData;
}LMX_STRUCT_REC_CTRL;


/////////////////////////////////////////////////
// 
// Tag definition: shooting execution system: AF / AE system
// 
/////////////////////////////////////////////////
enum Lmx_TagID_Rec_Ctrl_AFAE {
    LMX_DEF_LIB_TAG_REC_CTRL_AFAE_RESRV = LMX_DEF_LIB_EVENT_ID_REC_CTRL_AFAE,
    LMX_DEF_LIB_TAG_REC_CTRL_AFAE_LOCK_AE,                      // 0x03000021h AFAE control: AE lock request               
    LMX_DEF_LIB_TAG_REC_CTRL_AFAE_LOCK_AF,                      // 0x03000022h AFAE control: AF lock request               
    LMX_DEF_LIB_TAG_REC_CTRL_AFAE_LOCK_AFAE,                    // 0x03000023h AFAE control: AFAE lock request             
    LMX_DEF_LIB_TAG_REC_CTRL_AFAE_AF_ONESHOT,                   // 0x03000024h AFAE control: One-shot AF request          
    LMX_DEF_LIB_TAG_REC_CTRL_AFAE_LOCK_CLEAR,                   // 0x03000025h AFAE control: Unlock request               
    LMX_DEF_LIB_TAG_REC_CTRL_AFAE_MAX
};


//////////////////////////////////////////////////////////////////////////////////////////////////
// 																								//
// REC_CTRL_ZOOM                  																//
// 																								//
//////////////////////////////////////////////////////////////////////////////////////////////////
#pragma region REC_CTRL_ZOOM

/////////////////////////////////////////////////
// 
// Tag definition: shooting execution system: power zoom control
// 
/////////////////////////////////////////////////
enum Lmx_TagID_Rec_Ctrl_Zoom {
    LMX_DEF_LIB_TAG_REC_CTRL_ZOOM_RESRV = LMX_DEF_LIB_EVENT_ID_REC_CTRL_ZOOM,
    LMX_DEF_LIB_TAG_REC_CTRL_ZOOM_START_REQ,
    LMX_DEF_LIB_TAG_REC_CTRL_ZOOM_STOP_REQ,
    LMX_DEF_LIB_TAG_REC_CTRL_ZOOM_MAX
};

/////////////////////////////////////////////////
//
// Definition related to shooting control information: Definition of electric zoom direction setting value
//
////////////////////////////////////////////////
enum Lmx_def_lib_rec_ctrl_Zoom_Dir_param {
    LMX_DEF_REC_CTRL_ZOOM_DIR_WIDE,
    LMX_DEF_REC_CTRL_ZOOM_DIR_TELE,
};

/////////////////////////////////////////////////
//
// Shooting control information related definition: electric zoom speed setting value definition
//
////////////////////////////////////////////////
enum Lmx_def_lib_rec_ctrl_Zoom_Speed_param {
    LMX_DEF_REC_CTRL_ZOOM_SPEED_OFF,
    LMX_DEF_REC_CTRL_ZOOM_SPEED_LOW,
    LMX_DEF_REC_CTRL_ZOOM_SPEED_HIGH,
    LMX_DEF_REC_CTRL_ZOOM_SPEED_MID,
    LMX_DEF_REC_CTRL_ZOOM_SPEED_LOWER,
    LMX_DEF_REC_CTRL_ZOOM_SPEED_HIGHER,
};

#pragma endregion


/////////////////////////////////////////////////
// 
// Tag definition: shooting execution system: lens control execution system
// 
/////////////////////////////////////////////////
enum Lmx_TagID_Rec_Ctrl_Lens {
    LMX_DEF_LIB_TAG_REC_CTRL_LENS = LMX_DEF_LIB_EVENT_ID_REC_CTRL_LENS,
    LMX_DEF_LIB_TAG_REC_CTRL_LENS_MF_BAR,
    LMX_DEF_LIB_TAG_REC_CTRL_LENS_MAX,
};

// MF focus adjustment setting value
typedef enum Lmx_def_lib_rec_ctrl_lens_mf_pint_adjust {
    LMX_DEF_REC_CTRL_LENS_MF_PINTO_ADJUST_STOP = 0,					// MF focus adjustment:Stop
    LMX_DEF_REC_CTRL_LENS_MF_PINTO_ADJUST_FAR_FAST,					// MF focus adjustment:FAR Direction: Speed: Fast (*1)
    LMX_DEF_REC_CTRL_LENS_MF_PINTO_ADJUST_FAR_SLOW,					// MF focus adjustment:FAR Direction: Speed: Slow (*2)
    LMX_DEF_REC_CTRL_LENS_MF_PINTO_ADJUST_NEAR_SLOW,				// MF focus adjustment:NEAR Direction: Speed: Slow (*2)
    LMX_DEF_REC_CTRL_LENS_MF_PINTO_ADJUST_NEAR_FAST,				// MF focus adjustment:NEAR Direction: Speed: Fast (*1)
}LMX_DEF_REC_CTRL_LENS_MF_PINTO_ADJUST;


/////////////////////////////////////////////////
//
// Movie Menu Information Related Definition: Creative Movie Mode Related Definition
//
////////////////////////////////////////////////
enum Lmx_def_lib_Movie_Config_C_Movie_Mode {
    LMX_DEF_MOV_CFG_C_MOVIE_MODE_P = 0,
    LMX_DEF_MOV_CFG_C_MOVIE_MODE_A,
    LMX_DEF_MOV_CFG_C_MOVIE_MODE_S,
    LMX_DEF_MOV_CFG_C_MOVIE_MODE_M
};

/////////////////////////////////////////////////
//
// Movie Menu information Related definition: HDMI setting related definition
//
////////////////////////////////////////////////
enum Lmx_def_lib_Movie_Config_HDMI_Mode {
    LMX_DEF_MOV_CFG_HDMI_DOWNCVT_OFF = 0,			// MOVIE MENU setting: HDMI setting: OFF    Output with resolution of movie image quality setting
    LMX_DEF_MOV_CFG_HDMI_DOWNCVT_ON,				// MOVIE MENU setting: HDMI setting: ON     Movie image quality setting fixed at 1080p at 4K/C4K
    LMX_DEF_MOV_CFG_HDMI_DOWNCVT_AUTO				// MOVIE MENU setting: HDMI setting: AUTO   Movie image quality setting 4K/C4K, 4K/C4K for compatible TV, 1080p for non-compatible TV
};

/////////////////////////////////////////////////
//
// Movie Menu Information Related Definition: Quality Setting Related Definition
//
////////////////////////////////////////////////
enum Lmx_def_lib_movie_info_Quality_param {
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_PSH = 0,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_FSH,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_FH,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_CX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_CA,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_SH,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_H,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_L,
    LMX_DEF_MOV_CFG_QUALITY_10,
    LMX_DEF_MOV_CFG_QUALITY_30,
    LMX_DEF_MOV_CFG_QUALITY_VGA_10,
    LMX_DEF_MOV_CFG_QUALITY_VGA_30,
    LMX_DEF_MOV_CFG_QUALITY_WVGA_10,
    LMX_DEF_MOV_CFG_QUALITY_WVGA_30,
    LMX_DEF_MOV_CFG_QUALITY_HDTV_10,
    LMX_DEF_MOV_CFG_QUALITY_HDTV_30,
    LMX_DEF_MOV_CFG_QUALITY_HIGHSPEED,
    LMX_DEF_MOV_CFG_QUALITY_FHDV_30,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_60P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_50P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_25P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD_60P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD_25P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD_23P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_VGA,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_FUH,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_FPH,
    LMX_DEF_MOV_CFG_QUALITY_MOV_PSH,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FSA,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FSX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_CA,
    LMX_DEF_MOV_CFG_QUALITY_MOV_CX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_SH,
    LMX_DEF_MOV_CFG_QUALITY_MOV_SLOWQUICK,
    LMX_DEF_MOV_CFG_QUALITY_MP4_SLOWQUICK,
    LMX_DEF_MOV_CFG_QUALITY_MP4_4K_24P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_4K_24P_100MBPS,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_30P_100MBPS,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_25P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_30P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_25P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_24P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_23P_100MBPS,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_23P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_60P_200MBPS_I,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_60P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_60P_50MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_30P_200MBPS_I,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_30P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_30P_50MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_24P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_24P_200MBPS_I,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_24P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_24P_50MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_23P_200MBPS_I,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_23P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_23P_50MBPS_IPB,

    LMX_DEF_MOV_CFG_QUALITY_MOV_4K_24P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_QFHD_30P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_QFHD_24P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_QFHD_23P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_60P_200MBPS_I,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_60P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_60P_50MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_30P_200MBPS_I,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_30P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_30P_50MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_24P_200MBPS_I,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_24P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_24P_50MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_23P_200MBPS_I,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_23P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_23P_50MBPS_IPB,

    LMX_DEF_MOV_CFG_QUALITY_MOV_HD_60P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_HD_30P_100MBPS_IPB,
    LMX_DEF_MOV_CFG_QUALITY_MOV_HD_23P_100MBPS_IPB,

    LMX_DEF_MOV_CFG_QUALITY_MP4_HIGHSPEED_VGA_360P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HIGHSPEED_VGA_240P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HIGHSPEED_HD_240P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HIGHSPEED_HD_120P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HIGHSPEED_FHD_120P,

    LMX_DEF_MOV_CFG_QUALITY_MP4_640_640_30P,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD_LIVEVIEW,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_LIVEVIEW,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_LIVEVIEW,

    LMX_DEF_MOV_CFG_QUALITY_AVCHD_PSH_EX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_FSH_EX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_FH_EX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_CX_EX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_CA_EX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_SH_EX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_H_EX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_L_EX,
    LMX_DEF_MOV_CFG_QUALITY_WVGA_10_EX,
    LMX_DEF_MOV_CFG_QUALITY_WVGA_30_EX,
    LMX_DEF_MOV_CFG_QUALITY_HDTV_10_EX,
    LMX_DEF_MOV_CFG_QUALITY_HDTV_30_EX,
    LMX_DEF_MOV_CFG_QUALITY_HIGHSPEED_EX,
    LMX_DEF_MOV_CFG_QUALITY_FHDTV_30_EX,

    LMX_DEF_MOV_CFG_QUALITY_10_EX,
    LMX_DEF_MOV_CFG_QUALITY_30_EX,
    LMX_DEF_MOV_CFG_QUALITY_VGA_10_EX,
    LMX_DEF_MOV_CFG_QUALITY_VGA_30_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_60P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_50P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FDH_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FDH_25P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD_60P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD_25P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HD_23P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_VGA_EX,

    LMX_DEF_MOV_CFG_QUALITY_AVCHD_FUH_EX,
    LMX_DEF_MOV_CFG_QUALITY_AVCHD_FPH_EX,

    LMX_DEF_MOV_CFG_QUALITY_MOV_PSH_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FSA_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FSX_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_CA_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_CX_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_SH_EX,

    LMX_DEF_MOV_CFG_QUALITY_MP4_SLOWQUICK_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_SLOWQUICK_EX,

    LMX_DEF_MOV_CFG_QUALITY_MP4_4K_24P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_4K_24P_100MBPS_EX,
    LMX_DEF_MOV_CFG_QUALITY_QFHD_30P_100MBPS_EX,

    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_25P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_30P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_25P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_24P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_23P_100MBPS_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_QFHD_23P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_60P_200MBPS_I_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_60P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_60P_50MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_30P_200MBPS_I_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_30P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_30P_50MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_24P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_24P_200MBPS_I_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_24P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_24P_50MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_23P_200MBPS_I_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_23P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_FHD_23P__50MBPS_IPB_EX,

    LMX_DEF_MOV_CFG_QUALITY_MOV_4K_24P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_QFHD_30P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_QFHD_24P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_QFHD_23P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_60P_200MBPS_I_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_60P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_60P__50MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_30P_200MBPS_I_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_30P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_30P__50MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_24P_200MBPS_I_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_24P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_24P__50MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_23P_200MBPS_I_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_23P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_FHD_23P__50MBPS_IPB_EX,

    LMX_DEF_MOV_CFG_QUALITY_MOV_HD_60P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_HD_30P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MOV_HD_23P_100MBPS_IPB_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HIGHSPEED_VGA_360P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HIGHSPEED_HD_240P_EX,
    LMX_DEF_MOV_CFG_QUALITY_MP4_HIGHSPEED_FHD_120P_EX,

    LMX_DEF_AVCHD_FHD_VFR_420,
    LMX_DEF_AVCHD_FHD_VFR_23P_420,
    LMX_DEF_MP4_QFHD_60P,
    LMX_DEF_MP4_QFHD_24P,
    LMX_DEF_MP4_QFHD_60P_10_TEST,
    LMX_DEF_MP4_QFHD_60P_8_TEST,
    LMX_DEF_MP4_QFHD_30P_10_TEST,
    LMX_DEF_MP4_QFHD_AAC_30P,
    LMX_DEF_MP4_QFHD_AAC_25P,
    LMX_DEF_MP4_QFHD_AAC_23P,
    LMX_DEF_MP4_FHD_23P,
    LMX_DEF_MP4_C4K_48P_420_150MBPS,
    LMX_DEF_MP4_C4K_23P_420_100MBPS,
    LMX_DEF_MP4_C4K_23P_422_150MBPS,
    LMX_DEF_MP4_C4K_23P_422_400MBPS,
    LMX_DEF_MP4_C4K_24P_422_150MBPS,
    LMX_DEF_MP4_C4K_24P_422_400MBPS,
    LMX_DEF_MP4_QFHD_60P_420_8_150MBPS_IPB,
    LMX_DEF_MP4_QFHD_30P_422_10_150MBPS_IP,
    LMX_DEF_MP4_QFHD_30P_422_10_400MBPS,
    LMX_DEF_MP4_QFHD_23P_422_10_150MBPS_IP,
    LMX_DEF_MP4_QFHD_23P_422_10_400MBPS,
    LMX_DEF_MP4_QFHD_24P_422_10_150MBPS_IP,
    LMX_DEF_MP4_QFHD_24P_422_10_400MBPS,
    LMX_DEF_MP4_QFHD_48P_420_8_150MBPS_IPB,
    LMX_DEF_MP4_ANAMO4K_60P_420_8_150MBPS_IPB,
    LMX_DEF_MP4_ANAMO4K_30P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_ANAMO4K_30P_422_10_150MBPS_IP,
    LMX_DEF_MP4_ANAMO4K_30P_422_10_400MBPS,
    LMX_DEF_MP4_ANAMO4K_23P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_ANAMO4K_23P_422_10_150MBPS_IP,
    LMX_DEF_MP4_ANAMO4K_23P_422_10_400MBPS,
    LMX_DEF_MP4_ANAMO4K_24P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_ANAMO4K_24P_422_10_150MBPS_IP,
    LMX_DEF_MP4_ANAMO4K_24P_422_10_400MBPS,
    LMX_DEF_MP4_FHD_60P_422_10_200MBPS,
    LMX_DEF_MP4_FHD_60P_422_10_100MBPS_IPB,
    LMX_DEF_MP4_FHD_30P_422_10_200MBPS,
    LMX_DEF_MP4_FHD_30P_422_10_100MBPS_IPB,
    LMX_DEF_MP4_FHD_23P_422_10_200MBPS,
    LMX_DEF_MP4_FHD_23P_422_10_100MBPS_IPB,
    LMX_DEF_MP4_FHD_24P_422_10_200MBPS,
    LMX_DEF_MP4_FHD_24P_422_10_100MBPS_IPB,
    LMX_DEF_MP4_C4K_VFR_48P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_QFHD_VFR_30P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_QFHD_VFR_23P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_QFHD_VFR_24P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_FHD_VFR_60P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_FHD_VFR_30P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_FHD_VFR_23P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_FHD_VFR_24P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_C4K_48P_150MBPS_IPB,
    LMX_DEF_MOV_C4K_23P_150MBPS_IP,
    LMX_DEF_MOV_C4K_23P_100MBPS_IPB,
    LMX_DEF_MOV_C4K_23P_400MBPS,
    LMX_DEF_MOV_C4K_24P_150MBPS_IP,
    LMX_DEF_MOV_C4K_24P_400MBPS,
    LMX_DEF_MOV_QFHD_60P_420_8_150MBPS_IPB,
    LMX_DEF_MOV_QFHD_30P_422_10_150MBPS_IP,
    LMX_DEF_MOV_QFHD_30P_422_10_400MBPS,
    LMX_DEF_MOV_QFHD_23P_422_10_150MBPS_IP,
    LMX_DEF_MOV_QFHD_23P_422_10_400MBPS,
    LMX_DEF_MOV_QFHD_24P_422_10_150MBPS_IP,
    LMX_DEF_MOV_QFHD_24P_422_10_400MBPS,
    LMX_DEF_MOV_QFHD_48P_420_8_150MBPS_IPB,
    LMX_DEF_MOV_ANAMO4K_60P_420_8_150MBPS_IPB,
    LMX_DEF_MOV_ANAMO4K_30P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_ANAMO4K_30P_422_10_150MBPS_IP,
    LMX_DEF_MOV_ANAMO4K_30P_422_10_400MBPS,
    LMX_DEF_MOV_ANAMO4K_23P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_ANAMO4K_23P_422_10_150MBPS_IP,
    LMX_DEF_MOV_ANAMO4K_23P_422_10_400MBPS,
    LMX_DEF_MOV_ANAMO4K_24P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_ANAMO4K_24P_422_10_150MBPS_IP,
    LMX_DEF_MOV_ANAMO4K_24P_422_10_400MBPS,
    LMX_DEF_MOV_FHD_60P_422_10_200MBPS,
    LMX_DEF_MOV_FHD_60P_422_10_100MBPS_IPB,
    LMX_DEF_MOV_FHD_30P_422_10_200MBPS,
    LMX_DEF_MOV_FHD_30P_422_10_100MBPS_IPB,
    LMX_DEF_MOV_FHD_23P_422_10_200MBPS,
    LMX_DEF_MOV_FHD_23P_422_10_100MBPS_IPB,
    LMX_DEF_MOV_FHD_24P_422_10_200MBPS,
    LMX_DEF_MOV_FHD_24P_422_10_100MBPS_IPB,
    LMX_DEF_MOV_C4K_VFR_48P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_QFHD_VFR_30P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_QFHD_VFR_23P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_QFHD_VFR_24P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_FHD_VFR_60P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_FHD_VFR_30P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_FHD_VFR_23P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_FHD_VFR_24P_420_8_100MBPS_IPB,
    LMX_DEF_HEVC_6K_30P_420_10_200MBPS_IP,
    LMX_DEF_HEVC_ANAMO6K_30P_420_10_200MBPS_IP,
    LMX_DEF_HEVC_ANAMO6K_23P_420_10_200MBPS_IP,
    LMX_DEF_HEVC_ANAMO6K_24P_420_10_200MBPS_IP,
    LMX_DEF_AVCHD_FHD_VFR_420_EX,
    LMX_DEF_AVCHD_FHD_VFR_23P_420_EX,
    LMX_DEF_MP4_QFHD_60P_EX,
    LMX_DEF_MP4_QFHD_24P_EX,
    LMX_DEF_MP4_QFHD_60P_10_TEST_EX,
    LMX_DEF_MP4_QFHD_60P_8_TEST_EX,
    LMX_DEF_MP4_QFHD_30P_10_TEST_EX,
    LMX_DEF_MP4_QFHD_AAC_30P_EX,
    LMX_DEF_MP4_QFHD_AAC_25P_EX,
    LMX_DEF_MP4_QFHD_AAC_23P_EX,
    LMX_DEF_MP4_FHD_23P_EX,
    LMX_DEF_MP4_C4K_48P_420_150MBPS_EX,
    LMX_DEF_MP4_C4K_23P_420_100MBPS_EX,
    LMX_DEF_MP4_C4K_23P_422_150MBPS_EX,
    LMX_DEF_MP4_C4K_23P_422_400MBPS_EX,
    LMX_DEF_MP4_C4K_24P_422_150MBPS_EX,
    LMX_DEF_MP4_C4K_24P_422_400MBPS_EX,
    LMX_DEF_MP4_QFHD_60P_420_8_150MBPS_IPB_EX,
    LMX_DEF_MP4_QFHD_30P_422_10_150MBPS_IP_EX,
    LMX_DEF_MP4_QFHD_30P_422_10_400MBPS_EX,
    LMX_DEF_MP4_QFHD_23P_422_10_150MBPS_IP_EX,
    LMX_DEF_MP4_QFHD_23P_422_10_400MBPS_EX,
    LMX_DEF_MP4_QFHD_24P_422_10_150MBPS_IP_EX,
    LMX_DEF_MP4_QFHD_24P_422_10_400MBPS_EX,
    LMX_DEF_MP4_QFHD_48P_420_8_150MBPS_IPB_EX,
    LMX_DEF_MP4_ANAMO4K_60P_420_8_150MBPS_IPB_EX,
    LMX_DEF_MP4_ANAMO4K_30P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_ANAMO4K_30P_422_10_150MBPS_IP_EX,
    LMX_DEF_MP4_ANAMO4K_30P_422_10_400MBPS_EX,
    LMX_DEF_MP4_ANAMO4K_23P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_ANAMO4K_23P_422_10_150MBPS_IP_EX,
    LMX_DEF_MP4_ANAMO4K_23P_422_10_400MBPS_EX,
    LMX_DEF_MP4_ANAMO4K_24P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_ANAMO4K_24P_422_10_150MBPS_IP_EX,
    LMX_DEF_MP4_ANAMO4K_24P_422_10_400MBPS_EX,
    LMX_DEF_MP4_FHD_60P_422_10_200MBPS_EX,
    LMX_DEF_MP4_FHD_60P_422_10_100MBPS_IPB_EX,
    LMX_DEF_MP4_FHD_30P_422_10_200MBPS_EX,
    LMX_DEF_MP4_FHD_30P_422_10_100MBPS_IPB_EX,
    LMX_DEF_MP4_FHD_23P_422_10_200MBPS_EX,
    LMX_DEF_MP4_FHD_23P_422_10_100MBPS_IPB_EX,
    LMX_DEF_MP4_FHD_24P_422_10_200MBPS_EX,
    LMX_DEF_MP4_FHD_24P_422_10_100MBPS_IPB_EX,
    LMX_DEF_MP4_C4K_VFR_48P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_QFHD_VFR_30P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_QFHD_VFR_23P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_QFHD_VFR_24P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_FHD_VFR_60P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_FHD_VFR_30P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_FHD_VFR_23P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_FHD_VFR_24P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_HIGHSPEED_VGA_240P_EX,
    LMX_DEF_MP4_HIGHSPEED_HD_120P_EX,
    LMX_DEF_MP4_640_640_30P_EX,
    LMX_DEF_MP4_HD_LIVEVIEW_EX,
    LMX_DEF_MP4_QFHD_LIVEVIEW_EX,
    LMX_DEF_MP4_FHD_LIVEVIEW_EX,
    LMX_DEF_MOV_C4K_48P_150MBPS_IPB_EX,
    LMX_DEF_MOV_C4K_23P_150MBPS_IP_EX,
    LMX_DEF_MOV_C4K_23P_100MBPS_IPB_EX,
    LMX_DEF_MOV_C4K_23P_400MBPS_EX,
    LMX_DEF_MOV_C4K_24P_150MBPS_IP_EX,
    LMX_DEF_MOV_C4K_24P_400MBPS_EX,
    LMX_DEF_MOV_QFHD_60P_420_8_150MBPS_IPB_EX,
    LMX_DEF_MOV_QFHD_30P_422_10_150MBPS_IP_EX,
    LMX_DEF_MOV_QFHD_30P_422_10_400MBPS_EX,
    LMX_DEF_MOV_QFHD_23P_422_10_150MBPS_IP_EX,
    LMX_DEF_MOV_QFHD_23P_422_10_400MBPS_EX,
    LMX_DEF_MOV_QFHD_24P_422_10_150MBPS_IP_EX,
    LMX_DEF_MOV_QFHD_24P_422_10_400MBPS_EX,
    LMX_DEF_MOV_QFHD_48P_420_8_150MBPS_IPB_EX,
    LMX_DEF_MOV_ANAMO4K_60P_420_8_150MBPS_IPB_EX,
    LMX_DEF_MOV_ANAMO4K_30P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_ANAMO4K_30P_422_10_150MBPS_IP_EX,
    LMX_DEF_MOV_ANAMO4K_30P_422_10_400MBPS_EX,
    LMX_DEF_MOV_ANAMO4K_23P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_ANAMO4K_23P_422_10_150MBPS_IP_EX,
    LMX_DEF_MOV_ANAMO4K_23P_422_10_400MBPS_EX,
    LMX_DEF_MOV_ANAMO4K_24P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_ANAMO4K_24P_422_10_150MBPS_IP_EX,
    LMX_DEF_MOV_ANAMO4K_24P_422_10_400MBPS_EX,
    LMX_DEF_MOV_FHD_60P_422_10_200MBPS_EX,
    LMX_DEF_MOV_FHD_60P_422_10_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_30P_422_10_200MBPS_EX,
    LMX_DEF_MOV_FHD_30P_422_10_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_23P_422_10_200MBPS_EX,
    LMX_DEF_MOV_FHD_23P_422_10_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_24P_422_10_200MBPS_EX,
    LMX_DEF_MOV_FHD_24P_422_10_100MBPS_IPB_EX,
    LMX_DEF_MOV_C4K_VFR_48P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_QFHD_VFR_30P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_QFHD_VFR_23P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_QFHD_VFR_24P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_VFR_60P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_VFR_30P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_VFR_23P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_VFR_24P_420_8_100MBPS_IPB_EX,
    LMX_DEF_HEVC_6K_30P_420_10_200MBPS_IP_EX,
    LMX_DEF_HEVC_ANAMO6K_30P_420_10_200MBPS_IP_EX,
    LMX_DEF_HEVC_ANAMO6K_23P_420_10_200MBPS_IP_EX,
    LMX_DEF_HEVC_ANAMO6K_24P_420_10_200MBPS_IP_EX,
    LMX_DEF_HEVC_QFHD_30P_420_10_72MBPS_IP,
    LMX_DEF_HEVC_QFHD_23P_420_10_72MBPS_IP,
    LMX_DEF_HEVC_QFHD_30P_420_10_72MBPS_IP_EX,
    LMX_DEF_HEVC_QFHD_23P_420_10_72MBPS_IP_EX,

    LMX_DEF_MP4_C4K_30P_422_10_400MBPS_ALL_I,
    LMX_DEF_MP4_C4K_60P_420_8_150MBPS_IPB,
    LMX_DEF_MP4_C4K_30P_420_8_100MBPS_IPB,
    LMX_DEF_MP4_C4K_30P_422_10_150MBPS_IP,
    LMX_DEF_MP4_C4K_30P_422_10_400MBPS_ALL_I_EX,
    LMX_DEF_MP4_C4K_60P_420_8_150MBPS_IPB_EX,
    LMX_DEF_MP4_C4K_30P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MP4_C4K_30P_422_10_150MBPS_IP_EX,
    LMX_DEF_MOV_C4K_30P_422_10_400MBPS_ALL_I,
    LMX_DEF_MOV_C4K_60P_420_8_150MBPS_IPB,
    LMX_DEF_MOV_C4K_30P_420_8_100MBPS_IPB,
    LMX_DEF_MOV_C4K_30P_422_10_150MBPS_IP,
    LMX_DEF_MOV_C4K_30P_422_10_400MBPS_ALL_I_EX,
    LMX_DEF_MOV_C4K_60P_420_8_150MBPS_IPB_EX,
    LMX_DEF_MOV_C4K_30P_420_8_100MBPS_IPB_EX,
    LMX_DEF_MOV_C4K_30P_422_10_150MBPS_IP_EX,

    LMX_DEF_MP4_QFHD_50P_420_8_150MBPS_IPB,
    LMX_DEF_MP4_QFHD_50P_420_8_150MBPS_IPB_EX,

	LMX_DEF_HEVC_QFHD_25P_420_10_72MBPS_IP,
	LMX_DEF_HEVC_QFHD_25P_420_10_72MBPS_IP_EX,
	LMX_DEF_MP4_HIGHSPEED_FHD_30P_150FPS,
	LMX_DEF_MP4_HIGHSPEED_FHD_25P_125FPS,
	LMX_DEF_MP4_HIGHSPEED_FHD_30P_120FPS,
	LMX_DEF_MP4_HIGHSPEED_FHD_25P_100FPS,
	LMX_DEF_MOV_QFHD_50P_420_8_150MBPS_IPB,
	LMX_DEF_MOV_QFHD_25P_422_10_150MBPS_IP,
	LMX_DEF_MOV_FHD_50P_422_10_100MBPS_IPB,
	LMX_DEF_MOV_FHD_25P_422_10_100MBPS_IPB,
	LMX_DEF_MOV_QFHD_50P_420_8_150MBPS_IPB_EX,
	LMX_DEF_MOV_QFHD_25P_422_10_150MBPS_IP_EX,
	LMX_DEF_MOV_FHD_50P_422_10_100MBPS_IPB_EX,
	LMX_DEF_MOV_FHD_25P_422_10_100MBPS_IPB_EX,
	LMX_DEF_MOV_C4K_60P_200MBPS_IPB,
	LMX_DEF_MOV_C4K_50P_200MBPS_IPB,
	LMX_DEF_MOV_C4K_47P_200MBPS_IPB,
	LMX_DEF_MOV_C4K_48P_200MBPS_IPB,
	LMX_DEF_MOV_QFHD_60P_420_10_200MBPS_IPB,
	LMX_DEF_MOV_QFHD_50P_420_10_200MBPS_IPB,
	LMX_DEF_MOV_QFHD_47P_420_10_200MBPS_IPB,
	LMX_DEF_MOV_QFHD_48P_420_10_200MBPS_IPB,
	LMX_DEF_MOV_ANAMO4K_60P_420_10_200MBPS_IPB,
	LMX_DEF_MOV_ANAMO4K_50P_420_10_200MBPS_IPB,
	LMX_DEF_MOV_ANAMO4K_47P_420_10_200MBPS_IPB,
	LMX_DEF_MOV_ANAMO4K_48P_420_10_200MBPS_IPB,
	LMX_DEF_MOV_FHD_60I_422_10_100MBPS_I,
	LMX_DEF_MOV_FHD_60I_422_10_50MBPS_IPB,
	LMX_DEF_MOV_FHD_50I_422_10_100MBPS_I,
	LMX_DEF_MOV_FHD_50I_422_10_50MBPS_IPB,
	LMX_DEF_MOV_FHD_120P_420_10_150MBPS_IPB,
	LMX_DEF_MOV_FHD_100P_420_10_150MBPS_IPB,
	LMX_DEF_MOV_FHD_47P_420_10_100MBPS_IPB,
	LMX_DEF_MOV_FHD_48P_420_10_100MBPS_IPB,
	LMX_DEF_MOV_HEVC_FULL_6K_23P_420_10_200MBPS_IP,
	LMX_DEF_MOV_HEVC_FULL_6K_24P_420_10_200MBPS_IP,
	LMX_DEF_MOV_HEVC_SEMI_FULL_5_4K_25P_420_10_200MBPS_IP,
	LMX_DEF_MOV_HEVC_SEMI_FULL_5_4K_30P_420_10_200MBPS_IP,
	LMX_DEF_MOV_HEVC_FULL_5_9K_23P_420_10_200MBPS_IP,
	LMX_DEF_MOV_HEVC_FULL_5_9K_24P_420_10_200MBPS_IP,
	LMX_DEF_MOV_HEVC_FULL_5_9K_25P_420_10_200MBPS_IP,
	LMX_DEF_MOV_HEVC_FULL_5_9K_30P_420_10_200MBPS_IP,
	LMX_DEF_HEVC_QFHD_60P_420_10_100MBPS_IPB,
	LMX_DEF_HEVC_QFHD_50P_420_10_100MBPS_IPB,
	LMX_DEF_MOV_C4K_60P_200MBPS_IPB_EX,
	LMX_DEF_MOV_C4K_50P_200MBPS_IPB_EX,
	LMX_DEF_MOV_C4K_47P_200MBPS_IPB_EX,
	LMX_DEF_MOV_C4K_48P_200MBPS_IPB_EX,
	LMX_DEF_MOV_QFHD_60P_420_10_200MBPS_IPB_EX,
	LMX_DEF_MOV_QFHD_50P_420_10_200MBPS_IPB_EX,
	LMX_DEF_MOV_QFHD_47P_420_10_200MBPS_IPB_EX,
	LMX_DEF_MOV_QFHD_48P_420_10_200MBPS_IPB_EX,
	LMX_DEF_MOV_ANAMO4K_60P_420_10_200MBPS_IPB_EX,
	LMX_DEF_MOV_ANAMO4K_50P_420_10_200MBPS_IPB_EX,
	LMX_DEF_MOV_ANAMO4K_47P_420_10_200MBPS_IPB_EX,
	LMX_DEF_MOV_ANAMO4K_48P_420_10_200MBPS_IPB_EX,
	LMX_DEF_MOV_FHD_60I_422_10_100MBPS_I_EX,
	LMX_DEF_MOV_FHD_60I_422_10_50MBPS_IPB_EX,
	LMX_DEF_MOV_FHD_50I_422_10_100MBPS_I_EX,
	LMX_DEF_MOV_FHD_50I_422_10_50MBPS_IPB_EX,
	LMX_DEF_MOV_FHD_120P_420_10_150MBPS_IPB_EX,
	LMX_DEF_MOV_FHD_100P_420_10_150MBPS_IPB_EX,
	LMX_DEF_MOV_FHD_47P_420_10_100MBPS_IPB_EX,
	LMX_DEF_MOV_FHD_48P_420_10_100MBPS_IPB_EX,
	LMX_DEF_MOV_HEVC_FULL_6K_23P_420_10_200MBPS_IP_EX,
	LMX_DEF_MOV_HEVC_FULL_6K_24P_420_10_200MBPS_IP_EX,
	LMX_DEF_MOV_HEVC_SEMI_FULL_5_4K_25P_420_10_200MBPS_IP_EX,
	LMX_DEF_MOV_HEVC_SEMI_FULL_5_4K_30P_420_10_200MBPS_IP_EX,
	LMX_DEF_MOV_HEVC_FULL_5_9K_23P_420_10_200MBPS_IP_EX,
	LMX_DEF_MOV_HEVC_FULL_5_9K_24P_420_10_200MBPS_IP_EX,
	LMX_DEF_MOV_HEVC_FULL_5_9K_25P_420_10_200MBPS_IP_EX,
	LMX_DEF_MOV_HEVC_FULL_5_9K_30P_420_10_200MBPS_IP_EX,
	LMX_DEF_HEVC_QFHD_60P_420_10_100MBPS_IPB_EX,
	LMX_DEF_HEVC_QFHD_50P_420_10_100MBPS_IPB_EX,
    LMX_DEF_MOV_QFHD_25P_100MBPS_IPB,
    LMX_DEF_MOV_FHD_50P_100MBPS_IPB,
    LMX_DEF_MOV_FHD_25P_100MBPS_IPB,
    LMX_DEF_MOV_QFHD_25P_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_50P_100MBPS_IPB_EX,
    LMX_DEF_MOV_FHD_25P_100MBPS_IPB_EX,

    LMX_DEF_MOV_CFG_QUALITY_MAX,
};

/////////////////////////////////////////////////
//
// Movie Menu information Related definition: Movie recording mode setting related definition
//
////////////////////////////////////////////////
enum Lmx_def_lib_movie_info_RecMode_param {
    LMX_DEF_MOV_CFG_REC_MODE_AVCHD,
    LMX_DEF_MOV_CFG_REC_MODE_MJPEG,
    LMX_DEF_MOV_CFG_REC_MODE_MP4,
    LMX_DEF_MOV_CFG_REC_MODE_MOV,
    LMX_DEF_MOV_CFG_REC_MODE_MP4_LPCM,
    LMX_DEF_MOV_CFG_REC_MODE_MP4_HEVC,
    LMX_DEF_MOV_CFG_REC_MODE_MAX
};


/////////////////////////////////////////////////
//
// Movie Menu: Movie Config Capability information storage structure
//
/////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_MOV_MENU_CONFIG_CAPA_INFO {			// Movie Config Capability information 
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_mov_cfg_C_movie_mode;	// Movie Config:C-Movie Mode capability information
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_mov_cfg_HDMI_mode;		// Movie Config:HDMI Mode capability information
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_mov_cfg_Quality_mode;	// Movie Config:Quality Mode capability information
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_mov_cfg_Rec_mode;		// Movie Config:Rec Mode capability information
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_mov_cfg_ExTelecon_mode;

    UINT16								CurVal_C_movie_mode;		// Movie Config:C-Movie Mode value
    UINT16								CurVal_HDMI_mode;			// Movie Config:HDMI Mode value
    UINT16								CurVal_Quality_mode;		// Movie Config:Quality Mode value
    UINT16								CurVal_Rec_Mode;			// Movie Config:Rec Mode value
    UINT16								CurVal_ExTelecon_Mode;
    LMX_STRUCT_PTP_FORM_ENUM_UINT16		Capa_mov_cfg_Photo_shot;
    UINT16								CurVal_Photo_shot;
}LMX_STRUCT_MOV_MENU_CONFIG_CAPA_INFO;


enum Lmx_def_lib_setup_cfg_filesetting_target_cfg {
    LMX_DEF_SETUP_CFG_FILE_TARGET_CFG_ONLY_CAMERA = 0,
    LMX_DEF_SETUP_CFG_FILE_TARGET_CFG_ONLY_PC,
    LMX_DEF_SETUP_CFG_FILE_TARGET_CFG_PC_AND_CAMERA,
    LMX_DEF_SETUP_CFG_FILE_TARGET_CFG_MAX,
};


/////////////////////////////////////////////////
//
// LiveView Stream Setting: Transmission Image Quality Setting data structure
//
////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG {
	UINT32		Resolution;
	UINT16		ObjFrameSize;
	UINT16		FrameRate;
}LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG;

/////////////////////////////////////////////////
//
// LiveView Stream Setting: Recommended Image quality List data structure
//
////////////////////////////////////////////////
typedef struct _tag_LMX_STRUCT_LIVEVIEW_STR_RECOM_IMG {
	UINT16									RecomImgQualityListCount;
	UINT16									RecomImgQualityStructSize;
	LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG		RecomImgQualityListData[1024];
}LMX_STRUCT_LIVEVIEW_STR_RECOM_IMG;


#define LMX_DEF_LIVEVIEW_STREAMDATA_SIZE_MAX	(1*1024*1024)

//--- Rotation information ---//
enum Lmx_Def_Liveview_Posture {
    LMX_DEF_LIVEVIEW_POSTURE_0 = 0,							// 0 degree (direction of 12 o'clock clock; camera is up)
    LMX_DEF_LIVEVIEW_POSTURE_90 = 1,						// 90 degrees (3 o'clock direction of the clock: the right side of the camera is down)
    LMX_DEF_LIVEVIEW_POSTURE_180 = 2,						// 180 degrees (direction of 6 o'clock of clock: camera is down)
    LMX_DEF_LIVEVIEW_POSTURE_270 = 3,						// 270 degrees (direction of 9 o'clock: the right side of the camera is up)
    LMX_DEF_LIVEVIEW_POSTURE_MAX,
};

// Histogram information
#define LMX_DEF_LIVEVIEW_HISTGRAM_ELEMENT_SIZE		    (64)


typedef struct _tag_LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM {
    UINT32				valid;
    UINT32				samples;
    UINT32				elems;
    UINT8				element[LMX_DEF_LIVEVIEW_HISTGRAM_ELEMENT_SIZE];
}LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM;

// Rotation information
typedef struct _tag_LMX_STRUCT_LIVEVIEW_INFO_POSTURE {
    UINT16				posture;
}LMX_STRUCT_LIVEVIEW_INFO_POSTURE;


// Level information
typedef struct _tag_LMX_STRUCT_LIVEVIEW_INFO_LEVEL {
    INT16				roll;
    INT16				pitch;
}LMX_STRUCT_LIVEVIEW_INFO_LEVEL;



#pragma pack(pop, Old)


#endif // _LMX_LIB_DEF_H__

