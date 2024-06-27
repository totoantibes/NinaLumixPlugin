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

public class LMX_STRUCT_APERTURE_CAPA_INFO : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal LMX_STRUCT_APERTURE_CAPA_INFO(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(LMX_STRUCT_APERTURE_CAPA_INFO obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(LMX_STRUCT_APERTURE_CAPA_INFO obj) {
    if (obj != null) {
      if (!obj.swigCMemOwn)
        throw new global::System.ApplicationException("Cannot release ownership as memory is not owned");
      global::System.Runtime.InteropServices.HandleRef ptr = obj.swigCPtr;
      obj.swigCMemOwn = false;
      obj.Dispose();
      return ptr;
    } else {
      return new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
    }
  }

  ~LMX_STRUCT_APERTURE_CAPA_INFO() {
    Dispose(false);
  }

  public void Dispose() {
    Dispose(true);
    global::System.GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwn) {
          swigCMemOwn = false;
          LumixDriverPINVOKE.delete_LMX_STRUCT_APERTURE_CAPA_INFO(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public SWIGTYPE_p_UINT16 CurVal {
    set {
      LumixDriverPINVOKE.LMX_STRUCT_APERTURE_CAPA_INFO_CurVal_set(swigCPtr, SWIGTYPE_p_UINT16.getCPtr(value));
      if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    } 
    get {
      SWIGTYPE_p_UINT16 ret = new SWIGTYPE_p_UINT16(LumixDriverPINVOKE.LMX_STRUCT_APERTURE_CAPA_INFO_CurVal_get(swigCPtr), true);
      if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
      return ret;
    } 
  }

  public LMX_STRUCT_PTP_FORM_RANGE_UINT16 CurVal_Range {
    set {
      LumixDriverPINVOKE.LMX_STRUCT_APERTURE_CAPA_INFO_CurVal_Range_set(swigCPtr, LMX_STRUCT_PTP_FORM_RANGE_UINT16.getCPtr(value));
    } 
    get {
      global::System.IntPtr cPtr = LumixDriverPINVOKE.LMX_STRUCT_APERTURE_CAPA_INFO_CurVal_Range_get(swigCPtr);
      LMX_STRUCT_PTP_FORM_RANGE_UINT16 ret = (cPtr == global::System.IntPtr.Zero) ? null : new LMX_STRUCT_PTP_FORM_RANGE_UINT16(cPtr, false);
      return ret;
    } 
  }

  public LMX_STRUCT_PTP_FORM_ENUM_UINT16 Capa_Enum {
    set {
      LumixDriverPINVOKE.LMX_STRUCT_APERTURE_CAPA_INFO_Capa_Enum_set(swigCPtr, LMX_STRUCT_PTP_FORM_ENUM_UINT16.getCPtr(value));
    } 
    get {
      global::System.IntPtr cPtr = LumixDriverPINVOKE.LMX_STRUCT_APERTURE_CAPA_INFO_Capa_Enum_get(swigCPtr);
      LMX_STRUCT_PTP_FORM_ENUM_UINT16 ret = (cPtr == global::System.IntPtr.Zero) ? null : new LMX_STRUCT_PTP_FORM_ENUM_UINT16(cPtr, false);
      return ret;
    } 
  }

  public LMX_STRUCT_APERTURE_CAPA_INFO() : this(LumixDriverPINVOKE.new_LMX_STRUCT_APERTURE_CAPA_INFO(), true) {
  }

}

}
