using System.Collections.Generic;

namespace LumixWrapper {

    /// <summary>
    /// Names the SDK's retError values. Transcribed from <c>enum Lmx_def_error_code</c> in
    /// <c>Library/LMX_lib_def.h</c>; the SDK ships these names and the plugin was logging raw decimal, which
    /// made a failure cost an hour of archaeology to read. The categories matter as much as the codes: a
    /// transport error (0x0004xxxx) says the link failed, a camera error (0x0010xxxx) says the body refused,
    /// and those lead to completely different places.
    /// </summary>
    public static class LumixError {

        private static readonly Dictionary<uint, string> Names = new Dictionary<uint, string> {
            { 0x00000000, "LMX_DEF_ERR_NO_ERROR (no error)" },
            { 0x00010000, "LMX_DEF_ERR_FUNC_PARAM (function argument: argument error)" },
            { 0x00010001, "LMX_DEF_ERR_FUNC_UNKNOWN (function argument: other error)" },
            { 0x00020000, "LMX_DEF_ERR_CB_INVALID_ID (callback: invalid ID)" },
            { 0x00020001, "LMX_DEF_ERR_CB_INVALID_FUNC (callback: function invalid)" },
            { 0x00020002, "LMX_DEF_ERR_CB_INVALID_PARAM (callback: illegal parameters)" },
            { 0x00020003, "LMX_DEF_ERR_CB_SAME_ID (callback: already registered with this ID)" },
            { 0x00020004, "LMX_DEF_ERR_CB_LIMIT (callback: registration limit reached)" },
            { 0x00020005, "LMX_DEF_ERR_CB_NOT_FIND (callback: ID not found)" },
            { 0x00020006, "LMX_DEF_ERR_CB_UNKNOWN (callback: other error)" },
            { 0x00030000, "LMX_DEF_ERR_DEV_DETECT (device: detection error)" },
            { 0x00030001, "LMX_DEF_ERR_DEV_OPEN (device: open error)" },
            { 0x00030002, "LMX_DEF_ERR_DEV_NEED_OPEN (device: not open, or disconnected)" },
            { 0x00030003, "LMX_DEF_ERR_DEV_UNKNOWN (device: other error)" },
            { 0x00040000, "LMX_DEF_ERR_COM_INVALID_PARAM (transport: parameter error)" },
            { 0x00040001, "LMX_DEF_ERR_COM_CMD (transport: command transmission error)" },
            { 0x00040002, "LMX_DEF_ERR_COM_DATA_SEND (transport: data transmission error)" },
            { 0x00040003, "LMX_DEF_ERR_COM_DATA_RCV (transport: data RECEPTION error - the command went out, nothing came back)" },
            { 0x00040004, "LMX_DEF_ERR_COM_DATA_BUSY (transport: busy)" },
            { 0x00040005, "LMX_DEF_ERR_COM_RES (transport: response error)" },
            { 0x00040006, "LMX_DEF_ERR_COM_MEM_ADD (transport: memory allocation error)" },
            { 0x00040007, "LMX_DEF_ERR_COM_UNKNOWN (transport: other error)" },
            { 0x00040008, "LMX_DEF_ERR_COM_THREAD (transport: thread creation failed)" },
            { 0x00040009, "LMX_DEF_ERR_COM_TIMEOUT (transport: communication timeout)" },
            { 0x0004000A, "LMX_DEF_ERR_COM_TIMEOUT_RECONNECT_OK (transport: timed out, reconnected)" },
            { 0x0004000B, "LMX_DEF_ERR_COM_TIMEOUT_RECONNECT_ERROR (transport: timed out, reconnect failed)" },
            { 0x00041000, "LMX_DEF_ERR_COM_SESSION_ALREADY_OPENED (session: already open)" },
            { 0x00041001, "LMX_DEF_ERR_COM_SESSION_NOT_OPENED (session: not opened yet)" },
            { 0x00041002, "LMX_DEF_ERR_COM_SESSION_NOT_SUPPORT (session: command not supported)" },
            { 0x00041003, "LMX_DEF_ERR_COM_SESSION_NOT_SUPPORT_VERSION (session: version not supported)" },
            { 0x00050000, "LMX_DEF_ERR_EVENT_RCV_UNKNOWN (event: unexpected event received)" },
            { 0x00050001, "LMX_DEF_ERR_EVENT_WAIT_TIMEOUT (event: wait timed out)" },
            { 0x00060000, "LMX_DEF_ERR_FILE (file system: general error)" },
            { 0x00060001, "LMX_DEF_ERR_FILE_PATH_LEN (file system: path too long)" },
            { 0x00060002, "LMX_DEF_ERR_FILE_OPEN (file system: open error)" },
            { 0x00060003, "LMX_DEF_ERR_FILE_SIZE (file system: size error)" },
            { 0x00060004, "LMX_DEF_ERR_FILE_READ (file system: read error)" },
            { 0x00060005, "LMX_DEF_ERR_FILE_TYPE_FWUP_UNKNOWN (file system: unknown firmware-file format)" },
            { 0x00070000, "LMX_DEF_ERR_MEM (memory: general error)" },
            { 0x00070001, "LMX_DEF_ERR_MEM_CREATE (memory: creation error)" },
            { 0x000F0000, "LMX_DEF_ERR_INTERNAL (internal error)" },
            { 0x000F0001, "LMX_DEF_ERR_INTERNAL_EXCEPTION (internal: exception inside the SDK, access violations included)" },
            { 0x000F0002, "LMX_DEF_ERR_DEV_FWUP_NOTREADY (internal: firmware update not ready, e.g. low battery)" },
            { 0x000F0003, "LMX_DEF_ERR_DEV_FWUP_ERROR (internal: firmware update error)" },
            { 0x000F0004, "LMX_DEF_ERR_DEV_FWUP_ERROR_VERSION (internal: firmware update version error)" },
            { 0x00100000, "LMX_DEF_ERR_CAM (camera command error)" },
            { 0x00100001, "LMX_DEF_ERR_CAM_INVALID_MODE (camera command: invalid mode)" },
        };

        /// <summary>The SDK's name and meaning for a retError value, or the raw value when it is not one we
        /// know. Always safe to call and never throws: it is only ever used to build a log line.</summary>
        public static string Describe(uint retError) {
            if (Names.TryGetValue(retError, out string known)) { return known; }
            return $"0x{retError:X8} ({retError}) - not a documented Lmx_def_error_code";
        }
    }
}
