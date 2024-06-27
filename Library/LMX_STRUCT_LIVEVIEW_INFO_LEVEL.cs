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

public class LMX_STRUCT_LIVEVIEW_INFO_LEVEL : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  protected bool swigCMemOwn;

  internal LMX_STRUCT_LIVEVIEW_INFO_LEVEL(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwn = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(LMX_STRUCT_LIVEVIEW_INFO_LEVEL obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  internal static global::System.Runtime.InteropServices.HandleRef swigRelease(LMX_STRUCT_LIVEVIEW_INFO_LEVEL obj) {
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

  ~LMX_STRUCT_LIVEVIEW_INFO_LEVEL() {
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
          LumixDriverPINVOKE.delete_LMX_STRUCT_LIVEVIEW_INFO_LEVEL(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public SWIGTYPE_p_INT16 roll {
    set {
      LumixDriverPINVOKE.LMX_STRUCT_LIVEVIEW_INFO_LEVEL_roll_set(swigCPtr, SWIGTYPE_p_INT16.getCPtr(value));
      if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    } 
    get {
      SWIGTYPE_p_INT16 ret = new SWIGTYPE_p_INT16(LumixDriverPINVOKE.LMX_STRUCT_LIVEVIEW_INFO_LEVEL_roll_get(swigCPtr), true);
      if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
      return ret;
    } 
  }

  public SWIGTYPE_p_INT16 pitch {
    set {
      LumixDriverPINVOKE.LMX_STRUCT_LIVEVIEW_INFO_LEVEL_pitch_set(swigCPtr, SWIGTYPE_p_INT16.getCPtr(value));
      if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
    } 
    get {
      SWIGTYPE_p_INT16 ret = new SWIGTYPE_p_INT16(LumixDriverPINVOKE.LMX_STRUCT_LIVEVIEW_INFO_LEVEL_pitch_get(swigCPtr), true);
      if (LumixDriverPINVOKE.SWIGPendingException.Pending) throw LumixDriverPINVOKE.SWIGPendingException.Retrieve();
      return ret;
    } 
  }

  public LMX_STRUCT_LIVEVIEW_INFO_LEVEL() : this(LumixDriverPINVOKE.new_LMX_STRUCT_LIVEVIEW_INFO_LEVEL(), true) {
  }

}

}
