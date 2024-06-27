//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.1.1
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------

namespace LumixDriverWrapper {

public class LumixDriver {
  public static void LMX_func_api_Init() {
    LumixDriverPINVOKE.LMX_func_api_Init();
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Close_Device(SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Close_Device(SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Get_PnPDeviceInfo(LMX_CONNECT_DEVICE_INFO plmxPnpDevInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Get_PnPDeviceInfo(LMX_CONNECT_DEVICE_INFO.getCPtr(plmxPnpDevInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Select_PnPDevice(SWIGTYPE_p_UINT32 dwDevIndex, LMX_CONNECT_DEVICE_INFO plmxPnpDevInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Select_PnPDevice(SWIGTYPE_p_UINT32.getCPtr(dwDevIndex), LMX_CONNECT_DEVICE_INFO.getCPtr(plmxPnpDevInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT32 LMX_func_api_Reg_NotifyCallback(SWIGTYPE_p_UINT32 CallBackType, SWIGTYPE_p_f_UINT32_UINT32__int appfunc) {
    SWIGTYPE_p_UINT32 ret = new SWIGTYPE_p_UINT32(LumixDriverPINVOKE.LMX_func_api_Reg_NotifyCallback(SWIGTYPE_p_UINT32.getCPtr(CallBackType), SWIGTYPE_p_f_UINT32_UINT32__int.getCPtr(appfunc)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT32 LMX_func_api_Delete_CallBackInfo(SWIGTYPE_p_UINT32 CallBackType) {
    SWIGTYPE_p_UINT32 ret = new SWIGTYPE_p_UINT32(LumixDriverPINVOKE.LMX_func_api_Delete_CallBackInfo(SWIGTYPE_p_UINT32.getCPtr(CallBackType)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_ISO_Get_Capability(LMX_STRUCT_ISO_CAPA_INFO pIsoCapaInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_ISO_Get_Capability(LMX_STRUCT_ISO_CAPA_INFO.getCPtr(pIsoCapaInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_ISO_Get_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_ISO_Get_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_ISO_Set_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_ISO_Set_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_ISO_Get_UpperLimit(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_ISO_Get_UpperLimit(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_ISO_Set_UpperLimit(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_ISO_Set_UpperLimit(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_SS_Get_Capability(LMX_STRUCT_SS_CAPA_INFO pSS_CapaInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_SS_Get_Capability(LMX_STRUCT_SS_CAPA_INFO.getCPtr(pSS_CapaInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_SS_Get_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_SS_Get_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_SS_Set_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_SS_Set_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_SS_Get_RangeLimit(SWIGTYPE_p_UINT32 pulMinParam, SWIGTYPE_p_UINT32 pulMaxParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_SS_Get_RangeLimit(SWIGTYPE_p_UINT32.getCPtr(pulMinParam), SWIGTYPE_p_UINT32.getCPtr(pulMaxParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_SS_Set_RangeLimit(SWIGTYPE_p_UINT32 ulMinParam, SWIGTYPE_p_UINT32 ulMaxParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_SS_Set_RangeLimit(SWIGTYPE_p_UINT32.getCPtr(ulMinParam), SWIGTYPE_p_UINT32.getCPtr(ulMaxParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Get_Capability(LMX_STRUCT_WB_CAPA_INFO pWB_CapaInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Get_Capability(LMX_STRUCT_WB_CAPA_INFO.getCPtr(pWB_CapaInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Set_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Set_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Set_KSet(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Set_KSet(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Set_ADJ_AB(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Set_ADJ_AB(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Set_ADJ_GM(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Set_ADJ_GM(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Set_ADJ_AB_Sep(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Set_ADJ_AB_Sep(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Get_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Get_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Get_KSet(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Get_KSet(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Get_ADJ_AB(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Get_ADJ_AB(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Get_ADJ_GM(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Get_ADJ_GM(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_WB_Get_ADJ_AB_Sep(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_WB_Get_ADJ_AB_Sep(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Aperture_Get_Capability(LMX_STRUCT_APERTURE_CAPA_INFO pAperture_CapaInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Aperture_Get_Capability(LMX_STRUCT_APERTURE_CAPA_INFO.getCPtr(pAperture_CapaInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Aperture_Get_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Aperture_Get_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Aperture_Get_RangeLimit(SWIGTYPE_p_UINT32 pulMinParam, SWIGTYPE_p_UINT32 pulMaxParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Aperture_Get_RangeLimit(SWIGTYPE_p_UINT32.getCPtr(pulMinParam), SWIGTYPE_p_UINT32.getCPtr(pulMaxParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Aperture_Set_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Aperture_Set_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Aperture_Set_RangeLimit(SWIGTYPE_p_UINT32 ulMinParam, SWIGTYPE_p_UINT32 ulMaxParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Aperture_Set_RangeLimit(SWIGTYPE_p_UINT32.getCPtr(ulMinParam), SWIGTYPE_p_UINT32.getCPtr(ulMaxParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_AF_Config_Get_Capability(LMX_STRUCT_AF_CONFIG_CAPA_INFO pAF_Cfg_CapaInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_AF_Config_Get_Capability(LMX_STRUCT_AF_CONFIG_CAPA_INFO.getCPtr(pAF_Cfg_CapaInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_AF_Config_Get_AF_Mode_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_AF_Config_Get_AF_Mode_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_AF_Config_Get_AF_Area_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_AF_Config_Get_AF_Area_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_AF_Config_Set_AF_Mode_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_AF_Config_Set_AF_Mode_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_AF_Config_Set_AF_Area_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_AF_Config_Set_AF_Area_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_AF_Config_Get_Quick_AF_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_AF_Config_Get_Quick_AF_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_AF_Config_Set_Quick_AF_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_AF_Config_Set_Quick_AF_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Exposure_Get_Capability(LMX_STRUCT_EXPOSURE_CAPA_INFO pExposure_CapaInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Exposure_Get_Capability(LMX_STRUCT_EXPOSURE_CAPA_INFO.getCPtr(pExposure_CapaInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Exposure_Get_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Exposure_Get_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Exposure_Get_RangeLimit(SWIGTYPE_p_UINT32 pulMinParam, SWIGTYPE_p_UINT32 pulMaxParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Exposure_Get_RangeLimit(SWIGTYPE_p_UINT32.getCPtr(pulMinParam), SWIGTYPE_p_UINT32.getCPtr(pulMaxParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Exposure_Set_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Exposure_Set_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Exposure_Set_RangeLimit(SWIGTYPE_p_UINT32 ulMinParam, SWIGTYPE_p_UINT32 ulMaxParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Exposure_Set_RangeLimit(SWIGTYPE_p_UINT32.getCPtr(ulMinParam), SWIGTYPE_p_UINT32.getCPtr(ulMaxParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Get_Capability(LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO pCameraMode_CapaInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Get_Capability(LMX_STRUCT_RECINFO_CAMERA_MODE_CAPA_INFO.getCPtr(pCameraMode_CapaInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Get_DriveMode(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Get_DriveMode(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Get_Mode_Pos(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Get_Mode_Pos(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Get_CreativeMode(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Get_CreativeMode(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Get_iA_Mode(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Get_iA_Mode(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Set_DriveMode(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Set_DriveMode(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Set_Mode_Pos(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Set_Mode_Pos(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Set_CreatvieMode(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Set_CreatvieMode(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_CameraMode_Set_iA_Mode(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_CameraMode_Set_iA_Mode(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Get_Capability(LMX_STRUCT_MOV_MENU_CONFIG_CAPA_INFO pMov_Cfg_CapaInfo, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Get_Capability(LMX_STRUCT_MOV_MENU_CONFIG_CAPA_INFO.getCPtr(pMov_Cfg_CapaInfo), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Get_C_Movie_mode_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Get_C_Movie_mode_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Get_HDMI_mode_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Get_HDMI_mode_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Get_Quality_mode_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Get_Quality_mode_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Get_Rec_mode_Param(SWIGTYPE_p_UINT32 pulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Get_Rec_mode_Param(SWIGTYPE_p_UINT32.getCPtr(pulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Set_C_Movie_mode_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Set_C_Movie_mode_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Set_HDMI_mode_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Set_HDMI_mode_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Set_Quality_mode_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Set_Quality_mode_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Mov_Config_Set_Rec_mode_Param(SWIGTYPE_p_UINT32 ulParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Mov_Config_Set_Rec_mode_Param(SWIGTYPE_p_UINT32.getCPtr(ulParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_SetupFilesConfig_Get_Target(SWIGTYPE_p_UINT16 punParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_SetupFilesConfig_Get_Target(SWIGTYPE_p_UINT16.getCPtr(punParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_SetupFilesConfig_Set_Target(SWIGTYPE_p_UINT16 unParam, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_SetupFilesConfig_Set_Target(SWIGTYPE_p_UINT16.getCPtr(unParam), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Rec_Ctrl_Release(LMX_STRUCT_REC_CTRL lpRecCtrl, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Rec_Ctrl_Release(LMX_STRUCT_REC_CTRL.getCPtr(lpRecCtrl), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Rec_Ctrl_AF_AE(LMX_STRUCT_REC_CTRL lpRecCtrl, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Rec_Ctrl_AF_AE(LMX_STRUCT_REC_CTRL.getCPtr(lpRecCtrl), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Rec_Ctrl_Zoom(LMX_STRUCT_REC_CTRL lpRecCtrl, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Rec_Ctrl_Zoom(LMX_STRUCT_REC_CTRL.getCPtr(lpRecCtrl), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Rec_Ctrl_Lens(LMX_STRUCT_REC_CTRL lpRecCtrl, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Rec_Ctrl_Lens(LMX_STRUCT_REC_CTRL.getCPtr(lpRecCtrl), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_MoveRec_Ctrl_Start(SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_MoveRec_Ctrl_Start(SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_MoveRec_Ctrl_Stop(SWIGTYPE_p_UINT8 stopmode, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_MoveRec_Ctrl_Stop(SWIGTYPE_p_UINT8.getCPtr(stopmode), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Get_Object(SWIGTYPE_p_UINT32 ObjectHandle, SWIGTYPE_p_UINT8 lpStoreBufAdder, SWIGTYPE_p_UINT32 StoreBufSize, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Get_Object(SWIGTYPE_p_UINT32.getCPtr(ObjectHandle), SWIGTYPE_p_UINT8.getCPtr(lpStoreBufAdder), SWIGTYPE_p_UINT32.getCPtr(StoreBufSize), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Get_Object_FormatType(SWIGTYPE_p_UINT32 ObjHandle, SWIGTYPE_p_UINT32 pFormatType, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Get_Object_FormatType(SWIGTYPE_p_UINT32.getCPtr(ObjHandle), SWIGTYPE_p_UINT32.getCPtr(pFormatType), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Get_Object_DataSize(SWIGTYPE_p_UINT32 ObjHandle, SWIGTYPE_p_UINT64 pDataSize, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Get_Object_DataSize(SWIGTYPE_p_UINT32.getCPtr(ObjHandle), SWIGTYPE_p_UINT64.getCPtr(pDataSize), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Get_Object_FileName(SWIGTYPE_p_UINT32 ObjHandle, LMX_STRUCT_PTP_ARRAY_STRING pFileName, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Get_Object_FileName(SWIGTYPE_p_UINT32.getCPtr(ObjHandle), LMX_STRUCT_PTP_ARRAY_STRING.getCPtr(pFileName), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Get_Partial_Object(SWIGTYPE_p_UINT32 ObjectHandle, SWIGTYPE_p_UINT8 lpStoreBufAdder, SWIGTYPE_p_UINT32 ui32_DataOffset, SWIGTYPE_p_UINT32 u32_SplitTransferBytes, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Get_Partial_Object(SWIGTYPE_p_UINT32.getCPtr(ObjectHandle), SWIGTYPE_p_UINT8.getCPtr(lpStoreBufAdder), SWIGTYPE_p_UINT32.getCPtr(ui32_DataOffset), SWIGTYPE_p_UINT32.getCPtr(u32_SplitTransferBytes), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Get_Partial_ObjectEx(SWIGTYPE_p_UINT32 ObjectHandle, SWIGTYPE_p_UINT8 lpStoreBufAdder, SWIGTYPE_p_UINT64 ui64_DataOffset, SWIGTYPE_p_UINT32 u32_SplitTransferBytes, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Get_Partial_ObjectEx(SWIGTYPE_p_UINT32.getCPtr(ObjectHandle), SWIGTYPE_p_UINT8.getCPtr(lpStoreBufAdder), SWIGTYPE_p_UINT64.getCPtr(ui64_DataOffset), SWIGTYPE_p_UINT32.getCPtr(u32_SplitTransferBytes), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Skip_Object_Transfer(SWIGTYPE_p_UINT32 ObjectHandle, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Skip_Object_Transfer(SWIGTYPE_p_UINT32.getCPtr(ObjectHandle), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Ctrl_LiveView_Start(SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Ctrl_LiveView_Start(SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Ctrl_LiveView_Stop(SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Ctrl_LiveView_Stop(SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Get_LiveView_data(LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM pHistBuf, SWIGTYPE_p_UINT32 pHistSize, LMX_STRUCT_LIVEVIEW_INFO_POSTURE pPostBuf, SWIGTYPE_p_UINT32 pPostSize, LMX_STRUCT_LIVEVIEW_INFO_LEVEL pLevelBuf, SWIGTYPE_p_UINT32 pLevelSize, SWIGTYPE_p_UINT8 pJpegBuf, SWIGTYPE_p_UINT32 pJpegSize, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Get_LiveView_data(LMX_STRUCT_LIVEVIEW_INFO_HISTGRAM.getCPtr(pHistBuf), SWIGTYPE_p_UINT32.getCPtr(pHistSize), LMX_STRUCT_LIVEVIEW_INFO_POSTURE.getCPtr(pPostBuf), SWIGTYPE_p_UINT32.getCPtr(pPostSize), LMX_STRUCT_LIVEVIEW_INFO_LEVEL.getCPtr(pLevelBuf), SWIGTYPE_p_UINT32.getCPtr(pLevelSize), SWIGTYPE_p_UINT8.getCPtr(pJpegBuf), SWIGTYPE_p_UINT32.getCPtr(pJpegSize), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Open_Session(SWIGTYPE_p_UINT32 ulConnectVer, SWIGTYPE_p_UINT32 pulDeviceConnectVer, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Open_Session(SWIGTYPE_p_UINT32.getCPtr(ulConnectVer), SWIGTYPE_p_UINT32.getCPtr(pulDeviceConnectVer), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_Close_Session(SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_Close_Session(SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_LiveView_Str_Get_Trans_Img(LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG pTransImg, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_LiveView_Str_Get_Trans_Img(LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG.getCPtr(pTransImg), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_LiveView_Str_Set_Trans_Img(LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG pTransImg, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_LiveView_Str_Set_Trans_Img(LMX_STRUCT_LIVEVIEW_STR_TRANS_IMG.getCPtr(pTransImg), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public static SWIGTYPE_p_UINT8 LMX_func_api_LiveView_Str_Get_Recom_Img(LMX_STRUCT_LIVEVIEW_STR_RECOM_IMG pRecomImg, SWIGTYPE_p_UINT32 retError) {
    SWIGTYPE_p_UINT8 ret = new SWIGTYPE_p_UINT8(LumixDriverPINVOKE.LMX_func_api_LiveView_Str_Get_Recom_Img(LMX_STRUCT_LIVEVIEW_STR_RECOM_IMG.getCPtr(pRecomImg), SWIGTYPE_p_UINT32.getCPtr(retError)), true);
    return ret;
  }

  public static readonly int LMX_DEF_OBJ_CARDLESS_TRNSFER_HDL = LumixDriverPINVOKE.LMX_DEF_OBJ_CARDLESS_TRNSFER_HDL_get();
  public static readonly int LMX_DEF_LIVEVIEW_STREAMDATA_SIZE_MAX = LumixDriverPINVOKE.LMX_DEF_LIVEVIEW_STREAMDATA_SIZE_MAX_get();
  public static readonly int LMX_DEF_LIVEVIEW_HISTGRAM_ELEMENT_SIZE = LumixDriverPINVOKE.LMX_DEF_LIVEVIEW_HISTGRAM_ELEMENT_SIZE_get();
  public static readonly int DEVINFO_DEF_ARRAY_MAX = LumixDriverPINVOKE.DEVINFO_DEF_ARRAY_MAX_get();
  public static readonly int DEVINFO_DEF_STRING_MAX = LumixDriverPINVOKE.DEVINFO_DEF_STRING_MAX_get();
  public static readonly int LMX_BOOL_TRUE = LumixDriverPINVOKE.LMX_BOOL_TRUE_get();
  public static readonly int LMX_BOOL_FALSE = LumixDriverPINVOKE.LMX_BOOL_FALSE_get();
  public static readonly int LMX_DEF_USER_PTP_ARRAY_MAX = LumixDriverPINVOKE.LMX_DEF_USER_PTP_ARRAY_MAX_get();
  public static readonly int LMX_DEF_USER_PTP_STRING_MAX = LumixDriverPINVOKE.LMX_DEF_USER_PTP_STRING_MAX_get();
}

}
