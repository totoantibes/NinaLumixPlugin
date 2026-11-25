// Prints the authoritative size of every struct in Panasonic's Lumix SDK headers, plus the
// offsets of the fields most likely to be marshalled wrong. Compiled against the real headers
// shipped in the plugin repo, so this is the native truth, not an inference from the source.
//
// Compared against Marshal.SizeOf of the C# equivalents. A managed struct smaller than its
// native counterpart means the SDK writes past the end of what the plugin allocated, which is
// a corrupted heap that surfaces later and somewhere else.
#include <stdio.h>
#include <stddef.h>
#include "Lmx_lib_type.h"
#include "LMX_lib_def.h"
#include "LMX_lib_dev_common.h"

#define SZ(s)      printf("SIZE\t%s\t%zu\n", #s, sizeof(s))
#define OFF(s, f)  printf("OFF\t%s.%s\t%zu\n", #s, #f, offsetof(s, f))

int main(void) {

    SZ(LMX_STRUCT_PTP_ARRAY_STRING);
    OFF(LMX_STRUCT_PTP_ARRAY_STRING, NumChars);
    OFF(LMX_STRUCT_PTP_ARRAY_STRING, StringChars);
    OFF(LMX_STRUCT_PTP_ARRAY_STRING, Available);

    SZ(LMX_STRUCT_PTP_FORM_ENUM_UINT16);
    OFF(LMX_STRUCT_PTP_FORM_ENUM_UINT16, NumOfVal);
    OFF(LMX_STRUCT_PTP_FORM_ENUM_UINT16, SupportVal);
    OFF(LMX_STRUCT_PTP_FORM_ENUM_UINT16, Available);

    SZ(LMX_STRUCT_PTP_FORM_ENUM_UINT32);
    OFF(LMX_STRUCT_PTP_FORM_ENUM_UINT32, NumOfVal);
    OFF(LMX_STRUCT_PTP_FORM_ENUM_UINT32, SupportVal);
    OFF(LMX_STRUCT_PTP_FORM_ENUM_UINT32, Available);

    SZ(LMX_STRUCT_PTP_FORM_RANGE_UINT16);
    SZ(LMX_STRUCT_PTP_FORM_RANGE_UINT32);

    SZ(LMX_STRUCT_SS_CAPA_INFO);
    OFF(LMX_STRUCT_SS_CAPA_INFO, Capa_Enum);
    OFF(LMX_STRUCT_SS_CAPA_INFO, CurVal_Range);
    OFF(LMX_STRUCT_SS_CAPA_INFO, CurVal);
    OFF(LMX_STRUCT_SS_CAPA_INFO, Capa_Enum_Cfg);
    OFF(LMX_STRUCT_SS_CAPA_INFO, CurVal_Cfg);

    SZ(LMX_STRUCT_ISO_CAPA_INFO);
    SZ(LMX_STRUCT_APERTURE_CAPA_INFO);
    SZ(LMX_STRUCT_EXPOSURE_CAPA_INFO);
    SZ(LMX_STRUCT_WB_CAPA_INFO);
    SZ(LMX_STRUCT_AF_CONFIG_CAPA_INFO);
    SZ(LMX_STRUCT_MOV_MENU_CONFIG_CAPA_INFO);
    SZ(LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO);
    SZ(LMX_STRUCT_REC_CTRL);
    SZ(LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM);
    SZ(LMX_STRUCT_LIVEVIEW_INFO_LEVEL);
    SZ(LMX_STRUCT_LIVEVIEW_INFO_POSTURE);
    SZ(LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG);
    SZ(LMX_STRUCT_LIVEVIEW_STR_RECOM_IMG);
    SZ(LMX_CONNECT_DEVICE_INFO);
    OFF(LMX_CONNECT_DEVICE_INFO, find_PnpDevice_Count);
    OFF(LMX_CONNECT_DEVICE_INFO, find_PnpDevice_IDs);
    OFF(LMX_CONNECT_DEVICE_INFO, find_PnpDevice_Info);
    SZ(LMX_DEV_INFO);

    // Pointer and scalar widths, for the P/Invoke signature comparison.
    printf("WIDTH\tvoid*\t%zu\n", sizeof(void *));
    printf("WIDTH\tUINT32\t%zu\n", sizeof(UINT32));
    printf("WIDTH\tUINT64\t%zu\n", sizeof(UINT64));
    return 0;
}
